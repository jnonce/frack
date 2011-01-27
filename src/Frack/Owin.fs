namespace Frack
open System
open System.Collections.Generic

module Owin =
  /// Creates an OWIN application from an Async computation.
  let create handler cancellationToken =
    Action<#IDictionary<string, #obj>, Action<string, #IDictionary<string, string>, #seq<#obj>>, Action<exn>>(
      fun request onCompleted onError ->
        Async.StartWithContinuations(handler request, onCompleted.Invoke, onError.Invoke, onError.Invoke,
          cancellationToken = cancellationToken))
    
module Request =
  open Frack.Collections

  /// Reads the request body into a buffer and invokes the onCompleted callback with the buffer.
  let readBody (requestBody: obj) =
    let requestBody = requestBody :?> AsyncSeq<ArraySegment<byte>> |> AsyncSeq.toSeq
    async {
      let! chunks = requestBody
      // Determine the total number of bytes read.
      let length = chunks |> Seq.fold (fun len chunk -> len + chunk.Count) 0 
      // Read the contents of the body segments into a local buffer.
      let buffer, _ = chunks |> Seq.fold (fun (bs, offset) chunk ->
        Buffer.BlockCopy(chunk.Array, chunk.Offset, bs, offset, chunk.Count)
        (bs, offset + chunk.Count)) ((Array.create length 0uy), 0)
      return buffer }