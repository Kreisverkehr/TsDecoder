# Kreisverkehr.TsDecoder

> **Sunset notice:** This fork is no longer actively maintained and will not be developed further. I started [DvbMetadataDecoder](https://github.com/Kreisverkehr/DvbMetadataDecoder) as a clean, independent implementation rather than continuing this fork, with the goal of keeping the decoder understandable and straightforward to improve. I would be glad if you chose to use it. It is an early work in progress, so protocol coverage is incomplete and its APIs may change. The original [Cinegy/TsDecoder project](https://github.com/Cinegy/TsDecoder) remains an alternative.

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

The decoder processes MPEG-2 transport-stream data and exposes tables, descriptors, and elementary streams. PAT and EIT sections can span multiple transport packets. EIT decoding covers present/following information and schedule tables, including their event descriptors.

## Build and publish

Build and test with the .NET 10 SDK:

```sh
dotnet test Cinegy.TsDecoder.sln
```

GitHub Actions publishes this fork to NuGet.org when a `v*` tag is pushed (for example, `v4.0.1`). Configure the repository secret `NUGET_API_KEY` with a NuGet.org API key that can publish `Kreisverkehr.TsDecoder`.

The original project and this fork are licensed under Apache-2.0. See [LICENSE](LICENSE) for the full text and [NOTICE](NOTICE) for the required attribution notice that must be retained in redistributions and derived package distributions.
