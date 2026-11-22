#!/usr/bin/env bash
# Checks the packed package the way consumers get it, which the tests can't: they reference the
# source generator as a project analyzer. Builds, outside the repo, two projects:
#   Shared  a class library referencing Hlibz.UseCases, holding one use case and its handler,
#   App     a console app referencing only Shared (so the package only indirectly),
# then runs App: AddUseCases must register Shared's handler wrapped in its pipeline, and an
# invalid use case must fail validation before the handler runs. AddUseCases throws for an
# assembly the generator didn't run on, so a generator that is missing from the package, doesn't
# load or doesn't reach a consumer fails the check. net10.0, the oldest target.
#
# Usage: check-packed-generator.sh <directory holding the .nupkg files>
set -euo pipefail

artifacts="$(cd "$1" && pwd)"
package="$(ls "$artifacts"/Hlibz.UseCases.[0-9]*.nupkg)"
version="$(basename "$package" .nupkg)"
version="${version#Hlibz.UseCases.}"

# The physical path: restore drops a project reference reached through a symlink (macOS /var).
work="$(cd "$(mktemp -d)" && pwd -P)"
trap 'rm -rf "$work"' EXIT
# Its own package cache: the packed version must never land in the machine-wide one.
export NUGET_PACKAGES="$work/packages"

cat > "$work/nuget.config" <<XML
<configuration>
  <packageSources>
    <clear />
    <add key="artifacts" value="$artifacts" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="artifacts">
      <package pattern="Hlibz.UseCases" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
XML

mkdir "$work/Shared" "$work/App"

cat > "$work/Shared/Shared.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Hlibz.UseCases" Version="$version" />
  </ItemGroup>
</Project>
XML

cat > "$work/Shared/Ping.cs" <<'CS'
using System.ComponentModel.DataAnnotations;
using Hlibz.Results;
using Hlibz.UseCases;

namespace Shared;

public sealed record PingCommand([Required, MaxLength(4)] string Text) : IUseCase;

public interface IPingCommandHandler : IUseCaseHandler<PingCommand, string>;

internal sealed class PingCommandHandler : IPingCommandHandler
{
    public Task<Result<string>> HandleAsync(PingCommand useCase, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<string>.Success($"pong {useCase.Text}"));
}
CS

cat > "$work/App/App.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
    <ProjectReference Include="../Shared/Shared.csproj" />
  </ItemGroup>
</Project>
XML

cat > "$work/App/Program.cs" <<'CS'
using Hlibz.Results;
using Hlibz.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Shared;

ServiceCollection services = new();
services.AddUseCases([typeof(PingCommand).Assembly]);
using ServiceProvider provider = services.BuildServiceProvider();
using IServiceScope scope = provider.CreateScope();
IPingCommandHandler handler = scope.ServiceProvider.GetRequiredService<IPingCommandHandler>();

Result<string> valid = await handler.HandleAsync(new PingCommand("ok"));
Result<string> invalid = await handler.HandleAsync(new PingCommand("too long"));
Console.WriteLine($"valid: {valid.Status} {valid.Value}; invalid: {invalid.Status} {string.Join(", ", invalid.ValidationErrors?.Keys ?? [])}");
return valid.IsSuccess && invalid.Status == ResultStatus.Invalid && invalid.ValidationErrors!.ContainsKey("Text") ? 0 : 1;
CS

echo "Checking Hlibz.UseCases $version from $artifacts"
dotnet run --project "$work/App/App.csproj"
