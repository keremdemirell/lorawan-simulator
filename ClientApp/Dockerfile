# Use the official image as a base for building the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Set the working directory
WORKDIR /app

# Copy the .csproj file and restore any dependencies (via `dotnet restore`)
COPY LoRaWanSim.csproj .
RUN dotnet restore

# Copy the rest of the code
COPY . .

# Build the application
RUN dotnet publish -c Release -o /out

# Use the official .NET runtime image to run the app
FROM mcr.microsoft.com/dotnet/runtime:9.0

# Set the working directory in the container
WORKDIR /app

# Copy the published output from the build image
COPY --from=build /out .

# Copy the appsettings.json file into the runtime container
COPY appsettings.json /app/

# Set the entry point to run the application
ENTRYPOINT ["dotnet", "LoRaWanSim.dll"]
