using EmberVox.Core.Logging;
using EmberVox.Core.Types;
using EmberVox.Rendering.Utils;
using Silk.NET.SPIRV.Reflect;
using Silk.NET.Vulkan;
using DescriptorType = Silk.NET.Vulkan.DescriptorType;
using Format = Silk.NET.Vulkan.Format;
using Result = Silk.NET.SPIRV.Reflect.Result;

namespace EmberVox.Rendering.ShaderReflection;

public class ShaderReflector : IDisposable
{
    public ShaderStageFlags StageFlags => (ShaderStageFlags)_reflectModule.ShaderStage;
    public string EntryPoint { get; }
    public byte[] CompiledShaderCode { get; }

    private readonly Reflect _reflect;
    private ReflectShaderModule _reflectModule;

    public unsafe ShaderReflector(Reflect reflect, ReadOnlySpan<byte> shaderCode)
    {
        _reflect = reflect;

        Logger.Info?.WriteLine("-----> Reflecting shader... <-----");

        _reflectModule = ReflectShaderCode(shaderCode);
        EntryPoint = new string((sbyte*)_reflectModule.EntryPointName);
        CompiledShaderCode = shaderCode.ToArray();

        Logger.Metric?.WriteLine($"-> Size: {shaderCode.Length} bytes");
        Logger.Metric?.WriteLine($"-> Stage: {StageFlags}");
        Logger.Metric?.WriteLine($"-> Entry point: {EntryPoint}");

        Logger.Info?.WriteLine("-----> Shader reflection: OK <-----");
    }

    public void Dispose()
    {
        _reflect.DestroyShaderModule(new Span<ReflectShaderModule>(ref _reflectModule));
    }

    public unsafe ReadOnlySpan<ShaderDescriptor> GetShaderDescriptors()
    {
        Span<uint> descriptorBindingCount = stackalloc uint[1];
        var reflectShaderModule = _reflectModule;
        _reflect.EnumerateDescriptorBindings(
            new ReadOnlySpan<ReflectShaderModule>(ref reflectShaderModule),
            descriptorBindingCount,
            null
        );

        var ppDescriptorBindings = stackalloc DescriptorBinding*[(int)descriptorBindingCount[0]];
        _reflect.EnumerateDescriptorBindings(
            new ReadOnlySpan<ReflectShaderModule>(ref reflectShaderModule),
            descriptorBindingCount,
            ppDescriptorBindings
        );

        Span<ShaderDescriptor> result = new ShaderDescriptor[descriptorBindingCount[0]];

        for (int i = 0; i < descriptorBindingCount[0]; i++)
        {
            var pDescriptor = ppDescriptorBindings[i];
            result[i] = new ShaderDescriptor
            {
                BindingIndex = pDescriptor->Binding,
                SetIndex = pDescriptor->Set,
                Stride = pDescriptor->Block.Size,
                Offset = pDescriptor->Block.Offset,
                BindingType = (DescriptorType)pDescriptor->DescriptorType,
                Name = new string((sbyte*)pDescriptor->Name),
            };
        }

        return result;
    }

    public unsafe ReadOnlySpan<ShaderVariable> GetInputVariables()
    {
        Span<uint> inputVariableCount = stackalloc uint[1];
        var reflectShaderModule = _reflectModule;
        _reflect.EnumerateInputVariables(
            new ReadOnlySpan<ReflectShaderModule>(ref reflectShaderModule),
            inputVariableCount,
            null
        );

        var ppInterfaceVariables = stackalloc InterfaceVariable*[(int)inputVariableCount[0]];
        _reflect.EnumerateInputVariables(
            new ReadOnlySpan<ReflectShaderModule>(ref reflectShaderModule),
            inputVariableCount,
            ppInterfaceVariables
        );

        Span<ShaderVariable> result = new ShaderVariable[inputVariableCount[0]];
        int writeIndex = 0;
        for (int i = 0; i < inputVariableCount[0]; i++)
        {
            var pVariable = ppInterfaceVariables[i];

            // Skip built-in variables JUST IN CAAAAAAAAAAAAASE there happens to be one
            if (pVariable->BuiltIn == 0) //pVariable->Location == 0xFFFFFFFF
            {
                Logger.Warning?.WriteLine("variable scanned is built in! listing properties...:");
                Logger.Warning?.WriteLine($"-> name: {new string((sbyte*)pVariable->Name)}");
                continue;
            }

            result[writeIndex++] = new ShaderVariable
            {
                Location = pVariable->Location,
                Format = (Format)pVariable->Format,
                Stride = FormatUtils.GetSpirvFormatSize(pVariable->Format),
                Name = new string((sbyte*)pVariable->Name),
            };
        }

        return result[..writeIndex];
    }

    public unsafe ReadOnlySpan<ShaderVariable> GetOutputVariables()
    {
        Span<uint> outputVariableCount = stackalloc uint[1];
        var reflectShaderModule = _reflectModule;
        _reflect.EnumerateOutputVariables(
            new ReadOnlySpan<ReflectShaderModule>(ref reflectShaderModule),
            outputVariableCount,
            null
        );

        var ppInterfaceVariables = stackalloc InterfaceVariable*[(int)outputVariableCount[0]];
        _reflect.EnumerateOutputVariables(
            new ReadOnlySpan<ReflectShaderModule>(ref reflectShaderModule),
            outputVariableCount,
            ppInterfaceVariables
        );

        Span<ShaderVariable> result = new ShaderVariable[outputVariableCount[0]];
        int writeIndex = 0;
        for (int i = 0; i < outputVariableCount[0]; i++)
        {
            var pVariable = ppInterfaceVariables[i];

            if (pVariable->BuiltIn == 0) //pVariable->Location == 0xFFFFFFFF
            {
                continue;
            }

            result[writeIndex++] = new ShaderVariable
            {
                Location = pVariable->Location,
                Format = (Format)pVariable->Format,
                Stride = FormatUtils.GetSpirvFormatSize(pVariable->Format),
                Name = new string((sbyte*)pVariable->Name),
            };
        }

        return result[..writeIndex];
    }

    public void Dump()
    {
        Logger.Debug?.WriteLine("Dumping reflector...");
        Logger.Metric?.WriteLine($"-> shader stage: {StageFlags}");
        Logger.Metric?.WriteLine($"-> shader entry point: {EntryPoint}");

        Logger.Metric?.WriteLine("Dumping DescriptorBindings...");
        foreach (var shaderDescriptor in GetShaderDescriptors())
            Logger.Metric?.WriteLine($"-> {shaderDescriptor}");

        Logger.Metric?.WriteLine("Dumping InputVariables...");
        foreach (var inputVariable in GetInputVariables())
            Logger.Metric?.WriteLine($"-> {inputVariable}");

        Logger.Metric?.WriteLine("Dumping OutputVariables...");
        foreach (var outputVariable in GetOutputVariables())
            Logger.Metric?.WriteLine($"-> {outputVariable}");

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

    /*
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
            ShaderBindings descriptorBinding = new ShaderBindings
            {
                BindingIndex = pDescriptor->Binding,
                SetIndex = pDescriptor->Set,
                Stride = pDescriptor->Block.Size,
                Offset = pDescriptor->Block.Offset,
                BindingType = (DescriptorType)pDescriptor->DescriptorType,
                Name = pDescriptor->Name,
            };

            Logger.Metric?.WriteLine(
                $"-> Binding \"{new string((sbyte*)descriptorBinding.Name)}\": set={descriptorBinding.SetIndex} binding={descriptorBinding.BindingIndex} type={descriptorBinding.BindingType}"
            );

            if (descriptorBinding.BindingType == DescriptorType.UniformBuffer)
            {
                totalUniformBufferSize += descriptorBinding.Stride;
            }

            DescriptorBindingsByName[new string((sbyte*)pDescriptor->Name)] = descriptorBinding;
        }
        UniformBufferSize = totalUniformBufferSize;
    }
    */

    /*
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
    */

    /*
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
    */
}
