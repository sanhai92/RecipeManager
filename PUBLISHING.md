# Publishing Recipe Manager

Every publish must create a new app version.

When preparing an update:

1. Increase the `<Version>` value in `RecipeManager.csproj`.
2. Use the same version in `build-release.ps1`, `.github/workflows/release.yml`, and the matching `Publish-x.y.z.cmd` file.
3. Add a matching entry in `Services/ReleaseNotesService.cs` so users can read what changed.
4. Push the source and a matching Git tag such as `v1.0.4`.

The installed app only sees updates from GitHub Releases with a higher version number than the version currently installed.

For this release, run:

```powershell
.\Publish-1.0.4.cmd
```

After GitHub Actions finishes, installed users can click **Check for updates** and should see version `1.0.4`.
