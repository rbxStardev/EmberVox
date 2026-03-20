using EmberVox.Core.Logging;
using EmberVox.Core.Types;
using EmberVox.Rendering.Utils;
using Silk.NET.Core.Native;
using Silk.NET.SPIRV.Reflect;
using Silk.NET.Vulkan;
using DescriptorType = Silk.NET.Vulkan.DescriptorType;
using Format = Silk.NET.Vulkan.Format;
using Result = Silk.NET.SPIRV.Reflect.Result;

namespace EmberVox.Rendering.ShaderReflection;

public class ShaderReflector : IDisposable
{
    private readonly Reflect _reflect;

    public IDictionary<string, ShaderBindings> DescriptorBindingsByName { get; } =
        new Dictionary<string, ShaderBindings>();

    /*
    public IDictionary<string, ShaderBindings> MemberBindingsByName { get; } =
        new Dictionary<string, ShaderBindings>();
        */

    public UIntPtr UniformBufferSize { get; private set; }
    public ShaderStageFlags StageFlags => (ShaderStageFlags)_reflectModule.ShaderStage;
    public unsafe byte* EntryPoint { get; }
    public byte[] CompiledShaderCode { get; }

    public IDictionary<string, ShaderVariable> InputVariablesByName { get; } =
        new Dictionary<string, ShaderVariable>();
    public IDictionary<string, ShaderVariable> OutputVariablesByName { get; } =
        new Dictionary<string, ShaderVariable>();

    private ReflectShaderModule _reflectModule;

    public unsafe ShaderReflector(Reflect reflect, ReadOnlySpan<byte> shaderCode)
    {
        _reflect = reflect;

        Logger.Info?.WriteLine($"-----> Reflecting shader ({shaderCode.Length} bytes)... <-----");

        _reflectModule = ReflectShaderCode(shaderCode);
        EntryPoint = _reflectModule.EntryPointName;
        CompiledShaderCode = shaderCode.ToArray();

        Logger.Metric?.WriteLine($"-> Stage: {StageFlags}");
        Logger.Metric?.WriteLine($"-> Entry point: {SilkMarshal.PtrToString((nint)EntryPoint)}");

        ReflectShaderBindings();
        ReflectShaderInputVariables();
        ReflectShaderOutputVariables();

        Logger.Info?.WriteLine($"-----> Shader reflection: OK <-----");
    }

    public void Dispose()
    {
        _reflect.DestroyShaderModule(new Span<ReflectShaderModule>(ref _reflectModule));
    }

    public unsafe void Dump()
    {
        Logger.Debug?.WriteLine("Dumping reflector...");
        Logger.Metric?.WriteLine($"-> shader uniform buffer size: {UniformBufferSize}");
        Logger.Metric?.WriteLine($"-> shader stage: {StageFlags}");
        Logger.Metric?.WriteLine(
            $"-> shader entry point: {SilkMarshal.PtrToString((nint)EntryPoint)}"
        );
        Logger.Metric?.WriteLine("Dumping DescriptorBindings...");
        foreach (KeyValuePair<string, ShaderBindings> keyValuePair in DescriptorBindingsByName)
            Logger.Metric?.WriteLine($"-> Binding {keyValuePair.Key}: {keyValuePair.Value}");
        /*
        Logger.Metric?.WriteLine("Dumping MemberBindings...");
        foreach (KeyValuePair<string, ShaderBindings> keyValuePair in MemberBindingsByName)
            Logger.Metric?.WriteLine($"-> Binding {keyValuePair.Key}: {keyValuePair.Value}");
            */
        Logger.Metric?.WriteLine("Dumping InputVariables...");
        foreach (KeyValuePair<string, ShaderVariable> keyValuePair in InputVariablesByName)
            Logger.Metric?.WriteLine($"-> Variable {keyValuePair.Key}: {keyValuePair.Value}");
        Logger.Metric?.WriteLine("Dumping OutputVariables...");
        foreach (KeyValuePair<string, ShaderVariable> keyValuePair in OutputVariablesByName)
            Logger.Metric?.WriteLine($"-> Variable {keyValuePair.Key}: {keyValuePair.Value}");

        Logger.Debug?.WriteLine("Dumped reflector successfully");
        Console.WriteLine();
    }

    private ReflectShaderModule ReflectShaderCode(ReadOnlySpan<byte> shaderCode)
    {
        using ManagedPointer<byte> shaderCodeManagedPointer = new(shaderCode.Length);
        shaderCode.CopyTo(shaderCodeManagedPointer.Span);

        ReflectShaderModule reflectionModule = default;
        if (
            _reflect.CreateShaderModule(
                (nuint)shaderCodeManagedPointer.Length,
                shaderCodeManagedPointer.Span,
                new Span<ReflectShaderModule>(ref reflectionModule)
            ) != Result.Success
        )
        {
            throw new Exception("Failed to reflect shader module");
        }

        return reflectionModule;
    }

    private unsafe void ReflectShaderBindings()
    {
        Span<uint> descriptorBindingCount = stackalloc uint[1];
        var reflectShaderModule = _reflectModule;
        _reflect.EnumerateDescriptorBindings(
            new ReadOnlySpan<ReflectShaderModule>(ref reflectShaderModule),
            descriptorBindingCount,
            null
        );

        DescriptorBinding** ppDescriptorBindings =
            stackalloc DescriptorBinding*[(int)descriptorBindingCount[0]];
        _reflect.EnumerateDescriptorBindings(
            new ReadOnlySpan<ReflectShaderModule>(ref reflectShaderModule),
            descriptorBindingCount,
            ppDescriptorBindings
        );
        Logger.Metric?.WriteLine($"-> Descriptor bindings reflected: {descriptorBindingCount[0]}");

        uint totalUniformBufferSize = 0;
        for (int i = 0; i < descriptorBindingCount[0]; i++)
        {
            DescriptorBinding* pDescriptor = ppDescriptorBindings[i];
            Logger.Metric?.WriteLine(
                $"-> Binding \"{new string((sbyte*)pDescriptor->Name)}\": set={pDescriptor->Set} binding={pDescriptor->Binding} type={(DescriptorType)pDescriptor->DescriptorType}"
            );
            ShaderBindings descriptorBinding = new ShaderBindings
            {
                BindingIndex = pDescriptor->Binding,
                Stride = pDescriptor->Block.Size,
                Offset = pDescriptor->Block.Offset,
                BindingType = (DescriptorType)pDescriptor->DescriptorType,
            };

            /*
            if (descriptorBinding.BindingType == DescriptorType.UniformBuffer)
            {
                totalUniformBufferSize += descriptorBinding.Size;

                for (int blockIndex = 0; blockIndex < pDescriptor->Block.MemberCount; blockIndex++)
                {
                    BlockVariable member = pDescriptor->Block.Members[blockIndex];

                    ShaderBindings memberBinding = new ShaderBindings
                    {
                        BindingIndex = pDescriptor->Binding,
                        SetIndex = pDescriptor->Set,
                        Size = member.PaddedSize,
                        Offset = member.Offset,
                        BindingType = DescriptorType.,
                    };

                    MemberBindingsByName[new string((sbyte*)member.Name)] = memberBinding;
                }
            }
            */

            UniformBufferSize = totalUniformBufferSize;

            DescriptorBindingsByName[new string((sbyte*)pDescriptor->Name)] = descriptorBinding;
        }
    }

    private unsafe void ReflectShaderInputVariables()
    {
        Span<uint> inputVariableCount = stackalloc uint[1];
        var reflectShaderModule = _reflectModule;
        _reflect.EnumerateInputVariables(
            new ReadOnlySpan<ReflectShaderModule>(ref reflectShaderModule),
            inputVariableCount,
            null
        );

        InterfaceVariable** ppInterfaceVariables =
            stackalloc InterfaceVariable*[(int)inputVariableCount[0]];
        _reflect.EnumerateInputVariables(
            new ReadOnlySpan<ReflectShaderModule>(ref reflectShaderModule),
            inputVariableCount,
            ppInterfaceVariables
        );
        Logger.Metric?.WriteLine($"-> Input variables reflected: {inputVariableCount[0]}");

        for (int i = 0; i < inputVariableCount[0]; i++)
        {
            InterfaceVariable* pVariable = ppInterfaceVariables[i];

            // Skip built-in variables JUST IN CAAAAAAAAAAAAASE there happens to be one
            if (pVariable->Location == 0xFFFFFFFF)
            {
                Logger.Debug?.WriteLine($"Skipping built-in input variable at index {i}.");
                continue;
            }

            ShaderVariable variable = new ShaderVariable
            {
                Location = pVariable->Location,
                Format = (Format)pVariable->Format,
                Stride = FormatUtils.GetSpirvFormatSize(pVariable->Format),
            };

            InputVariablesByName[new string((sbyte*)pVariable->Name)] = variable;
        }
    }

    private unsafe void ReflectShaderOutputVariables()
    {
        Span<uint> outputVariableCount = stackalloc uint[1];
        var reflectShaderModule = _reflectModule;
        _reflect.EnumerateOutputVariables(
            new ReadOnlySpan<ReflectShaderModule>(ref reflectShaderModule),
            outputVariableCount,
            null
        );

        InterfaceVariable** ppInterfaceVariables =
            stackalloc InterfaceVariable*[(int)outputVariableCount[0]];
        _reflect.EnumerateOutputVariables(
            new ReadOnlySpan<ReflectShaderModule>(ref reflectShaderModule),
            outputVariableCount,
            ppInterfaceVariables
        );
        Logger.Metric?.WriteLine($"-> Output variables reflected: {outputVariableCount[0]}");

        for (int i = 0; i < outputVariableCount[0]; i++)
        {
            InterfaceVariable* pVariable = ppInterfaceVariables[i];

            if (pVariable->Location == 0xFFFFFFFF)
            {
                Logger.Debug?.WriteLine($"Skipping built-in output variable at index {i}.");
                continue;
            }

            ShaderVariable variable = new ShaderVariable
            {
                Location = pVariable->Location,
                Format = (Format)pVariable->Format,
                Stride = FormatUtils.GetSpirvFormatSize(pVariable->Format),
            };

            OutputVariablesByName[new string((sbyte*)pVariable->Name)] = variable;
        }
    }
}
