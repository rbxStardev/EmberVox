using System.Runtime.InteropServices;
using EmberVox.Logging;
using Silk.NET.Vulkan.Extensions.EXT;

namespace EmberVox;

public unsafe class DefaultDebugContext : IDisposable
{
    public ExtDebugUtils DebugUtilsExtension { get; }
    public DebugUtilsMessengerEXT DebugMessenger { get; }

    private readonly Vk _vk;
    private readonly Instance _instance;

    public DefaultDebugContext(Vk vk, Instance instance)
    {
        _vk = vk;
        _instance = instance;

        if (!_vk.TryGetInstanceExtension(_instance, out ExtDebugUtils debugUtilsExtension))
            throw new Exception("Failed to get ExtDebugUtils extension");
        DebugUtilsExtension = debugUtilsExtension;

        DebugMessenger = CreateDebugMessenger();
    }

    private DebugUtilsMessengerEXT CreateDebugMessenger()
    {
        const DebugUtilsMessageSeverityFlagsEXT severityFlags =
            DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt
            | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt
            | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;

        const DebugUtilsMessageTypeFlagsEXT messageTypeFlags =
            DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
            | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt
            | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;

        DebugUtilsMessengerCreateInfoEXT debugUtilsMessengerCreateInfoExt = new()
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = severityFlags,
            MessageType = messageTypeFlags,
            PfnUserCallback = new PfnDebugUtilsMessengerCallbackEXT(DebugCallback),
        };

        if (
            DebugUtilsExtension.CreateDebugUtilsMessenger(
                _instance,
                &debugUtilsMessengerCreateInfoExt,
                null,
                out DebugUtilsMessengerEXT debugMessenger
            ) != Result.Success
        )
        {
            throw new Exception("Failed to create debug messenger");
        }

        return debugMessenger;
    }

    private static uint DebugCallback(
        DebugUtilsMessageSeverityFlagsEXT severity,
        DebugUtilsMessageTypeFlagsEXT type,
        DebugUtilsMessengerCallbackDataEXT* pCallbackData,
        void* pUserData
    )
    {
        switch (severity)
        {
            case DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt:
            {
                Logger.Debug?.WriteLine(
                    $"(Verbose) validation layer: type {type.ToString()} msg: {Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage)}"
                );
                break;
            }
            case DebugUtilsMessageSeverityFlagsEXT.InfoBitExt:
            {
                Logger.Info?.WriteLine(
                    $"(Info) validation layer: type {type.ToString()} msg: {Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage)}"
                );
                break;
            }
            case DebugUtilsMessageSeverityFlagsEXT.WarningBitExt:
            {
                Logger.Warning?.WriteLine(
                    $"(Warning) validation layer: type {type.ToString()} msg: {Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage)}"
                );
                break;
            }
            case DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt:
            {
                Logger.Error?.WriteLine(
                    $"(Error) validation layer: type {type.ToString()} msg: {Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage)}"
                );
                break;
            }
            case DebugUtilsMessageSeverityFlagsEXT.None:
            default:
            {
                Console.WriteLine(
                    $"validation layer: type {type.ToString()} msg: {Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage)}"
                );
                break;
            }
        }

        return Vk.False;
    }

    public void Dispose()
    {
        DebugUtilsExtension.DestroyDebugUtilsMessenger(_instance, DebugMessenger, null);
        DebugUtilsExtension.Dispose();

        GC.SuppressFinalize(this);
    }
}
