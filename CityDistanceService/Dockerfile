# Use the official image as a parent image.
# FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

# Set the working directory.
WORKDIR /app

# Copy csproj and restore dependencies.
# COPY *.csproj ./

# Copy the rest of the working directory contents into the container.
COPY . ./
RUN dotnet restore

# Build the app.
# RUN dotnet publish -c Release -o out
RUN dotnet publish ./CityDistanceService.csproj -c Release -o out

# Build runtime image.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# Expose port 5189 for the app.
EXPOSE 5189

# Start the app.
ENTRYPOINT ["dotnet", "CityDistanceService.dll"]
