namespace Frack
module Middleware =
  open System
  open System.Collections.Generic

  /// Logs the incoming request and the time to respond.
  let log app = fun (req: IDictionary<string, obj>) -> async {
    let sw = System.Diagnostics.Stopwatch.StartNew()
    let! status, headers, body = app req
    printfn "Received a %A request from %A. Responded in %i ms."
            req?METHOD req?PATH_INFO sw.ElapsedMilliseconds
    sw.Reset()
    return status, headers, body }

  /// Intercepts a request using the HEAD method and strips away the returned body from a GET response.
  let head app = fun (req: IDictionary<string, obj>) -> async {
    if (req?METHOD :?> string) <> "HEAD" then
      return! app req
    else
      req?METHOD <- "GET"
      let! status, headers, _ = app req
      return status, headers, Seq.empty }

  /// Intercepts a request and checks for use of X_HTTP_METHOD_OVERRIDE.
  let methodOverride app = fun (req: IDictionary<string, obj>) -> async {
    // Leave out POST, as that is the method we are overriding.
    let httpMethods = ["GET";"HEAD";"PUT";"DELETE";"OPTIONS";"PATCH"]
    let methd = req?METHOD :?> string
    let contentType = req?CONTENT_TYPE :?> string
    if methd <> "POST" || contentType <> "application/x-http-form-urlencoded" then
      return! app req
    else
      let! body = Request.readBody req?input
      let form = UrlEncoded.parseForm body
      let m = if isNotNullOrEmpty form?_method then form?_method
              elif req.ContainsKey("HTTP_X_HTTP_METHOD_OVERRIDE") then
                req?HTTP_X_HTTP_METHOD_OVERRIDE :?> string
              else methd
      let httpMethod = m.ToUpperInvariant()
      if httpMethods |> List.exists ((=) httpMethod) then
        req?methodoverride_original_method <- "POST" 
        req?METHOD <- httpMethod
        req?input <- async { return ArraySegment<_>(body) }
        req?form_urlencoded <- form
      return! app req }