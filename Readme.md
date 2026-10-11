# Kreisverkehr.TsDecoder

An **unofficial fork** of [Cinegy/TsDecoder](https://github.com/Cinegy/TsDecoder), maintained and published independently by Kreisverkehr. This project and the `Kreisverkehr.TsDecoder` NuGet package are not affiliated with, endorsed by, or published by Cinegy GmbH. For the original project and its official package, use the links above.

The fork keeps the existing `Cinegy.TsDecoder` .NET namespaces for API compatibility. Its separately named NuGet package is:

```sh
dotnet add package Kreisverkehr.TsDecoder
```

## Usage

Feed transport-stream data to the decoder as byte arrays, for example from a UDP packet or file:

```csharp
_tsDecoder.AddData(byteArrayData);
```

The decoder then exposes transport-stream tables and descriptors and allows access to individual elementary streams. For an example application, see [Cinegy TS Analyser](https://github.com/Cinegy/tsanalyser).

## Overview

The decoder processes MPEG-2 transport-stream data and exposes tables, descriptors, and elementary streams. EIT decoding covers present/following information and schedule tables, including sections that continue across packets and their event descriptors.

## Build and publish

Build and test with the .NET 10 SDK:

```sh
dotnet test Cinegy.TsDecoder.sln
```

GitHub Actions publishes this fork to NuGet.org when a `v*` tag is pushed (for example, `v4.0.1`). Configure the repository secret `NUGET_API_KEY` with a NuGet.org API key that can publish `Kreisverkehr.TsDecoder`.

The original project is licensed under Apache-2.0; see [LICENSE](LICENSE).
