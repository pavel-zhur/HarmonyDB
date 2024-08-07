# Normal build with SIMD and BlazorWebAssemblyJiterpreter enabled (.Net 8 RC 2 defaults)
dotnet publish --nologo --no-restore --configuration Release --output "bin\Publish"

# Prepare the build with SIMD and BlazorWebAssemblyJiterpreter disabled
(Get-Content OneShelf.Frontend.Web.csproj) -replace 'compatiblity', 'no' | Out-File OneShelf.Frontend.Web.csproj

# ReleaseCompat build with SIMD and BlazorWebAssemblyJiterpreter disabled
dotnet publish --nologo --no-restore --configuration Release --output "bin\PublishCompat"

# Combine builds
# Copy the 'wwwroot\_framework' folder contents from the 2nd build to 'wwwroot\_frameworkCompat' in the 1st build
Copy-Item -Path "bin\PublishCompat\wwwroot\_framework" -Destination "bin\Publish\wwwroot\_frameworkCompat" -Recurse

# If building a PWA app with server-worker-assets.js the service-worker script needs to be modified to also detect SIMD and cache the appropriate build
# Copy the service-worker-assets.js from the 2nd build to 'service-worker-assets-compat.js' of the 1st build
Copy-Item -Path "bin\PublishCompat\wwwroot\service-worker-assets.js" -Destination "bin\Publish\wwwroot\service-worker-assets-compat.js"
