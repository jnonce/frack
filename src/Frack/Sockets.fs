// Taken from http://t0yv0.blogspot.com/2011/11/f-web-server-from-sockets-and-up.html
module Frack.Sockets
#nowarn "40"

open System
open System.Net.Sockets
open Frack
open FSharp.Control
open FSharpx

exception SocketIssue of SocketError with
    override this.ToString() = string this.Data0

/// Wraps the Socket.xxxAsync logic into F# async logic.
let asyncDo op (args: A) select =
    Async.FromContinuations <| fun (ok, error, cancel) ->
        let k (args: A) =
            match args.SocketError with
            | SocketError.Success -> ok <| select args
            | e -> error <| SocketIssue e
        let rec finish cont value =
            remover.Dispose()
            cont value
        and remover : IDisposable =
            args.Completed.Subscribe
                ({ new IObserver<_> with
                    member x.OnNext(v) = finish k v
                    member x.OnError(e) = finish error e
                    member x.OnCompleted() =
                        finish cancel <| System.OperationCanceledException("Cancelling the workflow, because the Observable awaited has completed.")
                })
        if not (op args) then
            finish k args

let private bytesPerLong = 4
let private bitsPerByte = 8

type Socket with
    member x.AsyncAccept (args) =
        asyncDo x.AcceptAsync args (fun a -> a.AcceptSocket)

    member x.AsyncAcceptSeq (pool: BocketPool) =
        let rec loop () = asyncSeq {
            let args = pool.Take()
            let! socket = x.AsyncAccept(args)
            yield socket
            pool.Add(args)
            yield! loop ()
        }
        loop ()

    member x.AsyncReceive (args) =
        asyncDo x.ReceiveAsync args (fun a -> a.BytesTransferred)

    member x.AsyncReceiveSeq (pool: BocketPool) =
        let rec loop () = asyncSeq {
            let args = pool.Take()
            let! bytesRead = x.AsyncReceive(args)
            if bytesRead > 0 then
                let chunk = BS(args.Buffer.[args.Offset..args.Offset + bytesRead])
                pool.Add(args)
                yield chunk
                yield! loop ()
            else pool.Add(args)
        }
        loop ()

    member x.AsyncSend (args: A) =
        asyncDo x.SendAsync args ignore

    member x.AsyncSendSeq (data, pool: BocketPool) =
        let rec loop data = async {
            let! chunk = data
            match chunk with
            | Cons(bs: BS, rest) ->
                let args = pool.Take()
                System.Buffer.BlockCopy(bs.Array, bs.Offset, args.Buffer, args.Offset, bs.Count)
                do! x.AsyncSend(args)
                pool.Add(args)
                do! loop rest
            | Nil -> ()
        }
        loop data

    member x.AsyncDisconnect (pool: BocketPool) =
        let args = pool.Take()
        asyncDo x.DisconnectAsync args <| fun args ->
            try
                x.Shutdown(SocketShutdown.Send)
            finally
                x.Close()
                pool.Add(args)
