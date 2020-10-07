# Update-ServicePackInclude-Json

Originally build for Viewpoint Construction Management. A C# .net core console app that runs during software service pack builds. It ensures that libraries and report files changed as part of the build are included in the service pack installer. The app uses the Azure DevOps REST API to pull information about the build and changes in the repository. It updates the json file which dictates what is included in the service pack by both checking in a changeset to the API, and updating the file in the local context of the build before it happens.
