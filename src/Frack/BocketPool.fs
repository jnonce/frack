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

type BocketPool(count: int, ?bufferSize) =
    let mutable disposed = false
    let bufferSize = defaultArg bufferSize 0
    let queue = new BlockingCollection<_>(count)
    let buffer =
        if bufferSize > 0 then
            Array.zeroCreate<byte> (count * bufferSize)
        else null
    do for i in 0 .. count - 1 do
        let args = new A()
        if buffer <> null then
            args.SetBuffer(buffer, bufferSize * i, bufferSize)
        queue.Add(args)

    member x.Take() = queue.Take()

    member x.Add(args: A) =
        Contract.Requires(args <> null)
        Contract.Ensures(args <> null && (buffer = null || args.Count = bufferSize))
        if buffer <> null && args.Count <> bufferSize then
            args.SetBuffer(buffer, args.Offset, bufferSize)
        queue.Add(args)

    member x.Dispose() =
        x.Dispose(true)
        GC.SuppressFinalize(x)

    member private x.Dispose(disposing) =
        if not disposed then
            if disposing then
                queue.CompleteAdding()
                while queue.Count > 1 do
                    let args = queue.Take()
                    args.Dispose()
                queue.Dispose()
            disposed <- true

    interface IDisposable with
        member x.Dispose() = x.Dispose()
