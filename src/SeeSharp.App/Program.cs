// Dev entry point for `dotnet run --project src/SeeSharp.App [-- <course-path>] [--admin]`; the tool starts the app via `seesharp start`.
return await SeeSharp.App.AppHost.RunAsync(args.FirstOrDefault(a => a != "--admin"), port: 0, openBrowser: false, admin: args.Contains("--admin"), Console.Out);
