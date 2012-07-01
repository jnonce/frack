//----------------------------------------------------------------------------
//
// Copyright (c) 2011-2012 Ryan Riley (@panesofglass)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//----------------------------------------------------------------------------
namespace Frack

open System
open System.Collections.Concurrent
open System.Diagnostics.Contracts

type A = System.Net.Sockets.SocketAsyncEventArgs
type BS = FSharpx.ByteString

type BufferPool(totalBuffers: int, bufferSize) =
    let mutable disposed = false
    let buffer = Array.zeroCreate<byte> (totalBuffers * bufferSize)
    let queue = new BlockingCollection<_>(totalBuffers)
    do for i in 0 .. totalBuffers - 1 do
        queue.Add(bufferSize * i)

    member x.Take() = BS(buffer, queue.Take(), bufferSize)

    member x.Add(offset) = queue.Add(offset)

    member x.Dispose() =
        x.Dispose(true)
        GC.SuppressFinalize(x)

    member private x.Dispose(disposing) =
        if not disposed then
            if disposing then
                queue.CompleteAdding()
                queue.Dispose()
            disposed <- true

    interface IDisposable with
        member x.Dispose() = x.Dispose()
