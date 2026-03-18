using EmberVox.Core.Types;
using EmberVox.Rendering.Utils;
using Silk.NET.SPIRV.Reflect;
using DescriptorType = Silk.NET.SPIRV.Reflect.DescriptorType;
using Result = Silk.NET.SPIRV.Reflect.Result;

namespace EmberVox.Rendering.GraphicsPipeline;

public class ShaderReflector
{
    private readonly Reflect _reflect;

    public IDictionary<string, ShaderBindings> DescriptorBindingsByName { get; } =
        new Dictionary<string, ShaderBindings>();

    public IDictionary<string, ShaderBindings> MemberBindingsByName { get; } =
        new Dictionary<string, ShaderBindings>();

    public UIntPtr UniformBufferSize { get; private set; }
    public ShaderStageFlagBits StageFlags { get; }

    public IDictionary<string, ShaderVariable> InputVariablesByName { get; } =
        new Dictionary<string, ShaderVariable>();
    public IDictionary<string, ShaderVariable> OutputVariablesByName { get; } =
        new Dictionary<string, ShaderVariable>();

    private readonly ReflectShaderModule _reflectModule;

    public ShaderReflector(Reflect reflect, byte[] shaderCode)
    {
        _reflect = reflect;

        _reflectModule = ReflectShaderModule(shaderCode);
        StageFlags = _reflectModule.ShaderStage;
        ReflectShaderBindings();
        ReflectShaderInputVariables();
        ReflectShaderOutputVariables();
    }

    private ReflectShaderModule ReflectShaderModule(Span<byte> shaderCode)
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

        uint totalUniformBufferSize = 0;
        for (int i = 0; i < descriptorBindingCount[0]; i++)
        {
            DescriptorBinding* pDescriptor = ppDescriptorBindings[i];
            ShaderBindings descriptorBinding = new ShaderBindings
            {
                BindingIndex = pDescriptor->Binding,
                Size = pDescriptor->Block.Size,
                Offset = pDescriptor->Block.Offset,
            };

            switch (pDescriptor->DescriptorType)
            {
                case DescriptorType.UniformBuffer:
                {
                    descriptorBinding.BindingType = ShaderBindingType.UniformBuffer;
                    totalUniformBufferSize += descriptorBinding.Size;
                    break;
                }
            }

            DescriptorBindingsByName[new string((sbyte*)pDescriptor->Name)] = descriptorBinding;

            if (pDescriptor->DescriptorType != DescriptorType.UniformBuffer)
            {
                continue;
            }

            for (int blockIndex = 0; blockIndex < pDescriptor->Block.MemberCount; blockIndex++)
            {
                BlockVariable member = pDescriptor->Block.Members[blockIndex];

                ShaderBindings memberBinding = new ShaderBindings
                {
                    BindingIndex = pDescriptor->Binding,
                    SetIndex = pDescriptor->Set,
                    Size = member.PaddedSize,
                    Offset = member.Offset,
                    BindingType = ShaderBindingType.UniformBufferMember,
                };

                MemberBindingsByName[new string((sbyte*)member.Name)] = memberBinding;
            }
        }

        UniformBufferSize = totalUniformBufferSize;
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

        for (int i = 0; i < inputVariableCount[0]; i++)
        {
            InterfaceVariable* pVariable = ppInterfaceVariables[i];

            if (pVariable->Location == 0xFFFFFFFF)
            {
                continue;
            }

            ShaderVariable variable = new ShaderVariable
            {
                Location = pVariable->Location,
                Format = (uint)pVariable->Format,
                Size = FormatUtils.GetSpirvFormatSize(pVariable->Format),
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

        for (int i = 0; i < outputVariableCount[0]; i++)
        {
            InterfaceVariable* pVariable = ppInterfaceVariables[i];

            if (pVariable->Location == 0xFFFFFFFF)
            {
                continue;
            }

            ShaderVariable variable = new ShaderVariable
            {
                Location = pVariable->Location,
                Format = (uint)pVariable->Format,
                Size = FormatUtils.GetSpirvFormatSize(pVariable->Format),
            };

            OutputVariablesByName[new string((sbyte*)pVariable->Name)] = variable;
        }
    }
}
