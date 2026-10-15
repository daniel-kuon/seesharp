// Dev entry point for `dotnet run --project src/SeeSharp.App [-- <course-path>]`; the tool starts the app via `seesharp start`.
return await SeeSharp.App.AppHost.RunAsync(args.Length > 0 ? args[0] : null, port: 0, openBrowser: false, Console.Out);
