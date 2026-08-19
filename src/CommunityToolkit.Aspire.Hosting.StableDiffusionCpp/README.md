# CommunityToolkit.Aspire.Hosting.StableDiffusionCpp

An Aspire hosting integration for the official
[`stable-diffusion.cpp`](https://github.com/leejet/stable-diffusion.cpp) server images.

## Getting started

Install the package in an Aspire AppHost:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.StableDiffusionCpp
```

Add the server and a model hosted on Hugging Face:

```csharp
var stableDiffusion = builder.AddStableDiffusionCpp(
        "stable-diffusion",
        StableDiffusionCppImageVariant.Cuda)
    .WithGPUSupport()
    .WithVaeTiling();

stableDiffusion.AddHuggingFaceModel(
    "stabilityai/stable-diffusion-xl-base-1.0",
    "sd_xl_base_1.0.safetensors");
```

The model is downloaded before the container starts. Interrupted downloads resume from
the partial file, and subsequent starts reuse the cached file under
`.stable-diffusion-cpp/{resource-name}/models`.

The integration uses official `ghcr.io/leejet/stable-diffusion.cpp` images. Available
variants are `Cuda`, `CudaSpark`, `Vulkan`, `Sycl`, and `Musa`.

## Gated or private Hugging Face models

Provide a secret parameter containing a Hugging Face access token:

```csharp
var token = builder.AddParameter("hugging-face-token", secret: true);

var stableDiffusion = builder
    .AddStableDiffusionCpp("stable-diffusion")
    .WithHuggingFaceToken(token);

stableDiffusion.AddHuggingFaceModel(
    "organization/model",
    "model.safetensors");
```

## LoRA and upscalers

Place compatible LoRA files under:

```text
.stable-diffusion-cpp/{resource-name}/models/loras
```

Place high-resolution upscaler models under:

```text
.stable-diffusion-cpp/{resource-name}/models/upscalers
```

The Web UI and API are exposed through the resource's `http` endpoint.
