var builder = DistributedApplication.CreateBuilder(args);

builder.AddStableDiffusionCpp(
        "stable-diffusion",
        StableDiffusionCppImageVariant.Cuda)
    .WithGPUSupport()
    .WithVaeTiling()
    .AddHuggingFaceModel(
        "stabilityai/stable-diffusion-xl-base-1.0",
        "sd_xl_base_1.0.safetensors");

builder.Build().Run();
