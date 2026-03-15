using System.Runtime.InteropServices;
using EmberVox.Core.Logging;
using EmberVox.Rendering.ResourceManagement;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;

namespace EmberVox.Rendering.Contexts;

internal sealed class DefaultDebugContext : IResource
{
    public ExtDebugUtils DebugUtilsExtension { get; }
    public DebugUtilsMessengerEXT DebugMessenger { get; }

    private readonly Instance _instance;

    public DefaultDebugContext(Vk vk, Instance instance)
    {
        _instance = instance;

        if (!vk.TryGetInstanceExtension(_instance, out ExtDebugUtils debugUtilsExtension))
            throw new Exception("Failed to get ExtDebugUtils extension");

        DebugUtilsExtension = debugUtilsExtension;
        DebugMessenger = CreateDebugMessenger();
    }

    public void Dispose()
    {
        DebugUtilsExtension.DestroyDebugUtilsMessenger(
            _instance,
            DebugMessenger,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        DebugUtilsExtension.Dispose();

        GC.SuppressFinalize(this);
    }

    private unsafe DebugUtilsMessengerEXT CreateDebugMessenger()
    {
        const DebugUtilsMessageSeverityFlagsEXT severityFlags =
            DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt
            | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt
            | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;

        const DebugUtilsMessageTypeFlagsEXT messageTypeFlags =
            DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
            | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt
            | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;

        DebugUtilsMessengerCreateInfoEXT createInfo = new()
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = severityFlags,
            MessageType = messageTypeFlags,
            PfnUserCallback = new PfnDebugUtilsMessengerCallbackEXT(DebugCallback),
        };

        DebugUtilsMessengerEXT debugMessenger = default;
        if (
            DebugUtilsExtension.CreateDebugUtilsMessenger(
                _instance,
                new ReadOnlySpan<DebugUtilsMessengerCreateInfoEXT>(ref createInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<DebugUtilsMessengerEXT>(ref debugMessenger)
            ) != Result.Success
        )
            throw new Exception("Failed to create debug messenger");

        return debugMessenger;
    }

    private static unsafe uint DebugCallback(
        DebugUtilsMessageSeverityFlagsEXT severity,
        DebugUtilsMessageTypeFlagsEXT type,
        DebugUtilsMessengerCallbackDataEXT* pCallbackData,
        void* pUserData
    )
    {
        string message =
            $"validation layer: type {type} msg: {Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage)}";

        switch (severity)
        {
            case DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt:
                Logger.Debug?.WriteLine($"(Verbose) {message}");
                break;
            case DebugUtilsMessageSeverityFlagsEXT.InfoBitExt:
                Logger.Info?.WriteLine($"(Info) {message}");
                break;
            case DebugUtilsMessageSeverityFlagsEXT.WarningBitExt:
                Logger.Warning?.WriteLine($"(Warning) {message}");
                break;
            case DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt:
                Logger.Error?.WriteLine($"(Error) {message}");
                break;
            case DebugUtilsMessageSeverityFlagsEXT.None:
            default:
                Console.WriteLine(message);
                break;
        }

        return Vk.False;
    }
}
