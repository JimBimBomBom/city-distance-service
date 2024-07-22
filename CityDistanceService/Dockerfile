# Use the official image as a parent image.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

# Set the working directory.
WORKDIR /app

# Copy the rest of the working directory contents into the container.
COPY . ./
RUN dotnet restore

# Build the app.
RUN dotnet publish ./CityDistanceService.csproj -c Release -o out

# Build runtime image.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# Expose port 8080 for the app.
EXPOSE 8080

# Start the app.
ENTRYPOINT ["dotnet", "CityDistanceService.dll"]
