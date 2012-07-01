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
