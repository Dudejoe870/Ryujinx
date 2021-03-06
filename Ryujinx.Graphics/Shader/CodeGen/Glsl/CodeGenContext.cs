using System.Collections.Generic;
using System.Text;

namespace Ryujinx.Graphics.Shader.CodeGen.Glsl
{
    class CodeGenContext
    {
        private const string Tab = "    ";

        public ShaderConfig Config { get; }

        public List<CBufferDescriptor> CBufferDescriptors { get; }
        public List<TextureDescriptor> TextureDescriptors { get; }

        public OperandManager OperandManager { get; }

        private StringBuilder _sb;

        private int _level;

        private string _indentation;

        public CodeGenContext(ShaderConfig config)
        {
            Config = config;

            CBufferDescriptors = new List<CBufferDescriptor>();
            TextureDescriptors = new List<TextureDescriptor>();

            OperandManager = new OperandManager();

            _sb = new StringBuilder();
        }

        public void AppendLine()
        {
            _sb.AppendLine();
        }

        public void AppendLine(string str)
        {
            _sb.AppendLine(_indentation + str);
        }

        public string GetCode()
        {
            return _sb.ToString();
        }

        public void EnterScope()
        {
            AppendLine("{");

            _level++;

            UpdateIndentation();
        }

        public void LeaveScope(string suffix = "")
        {
            if (_level == 0)
            {
                return;
            }

            _level--;

            UpdateIndentation();

            AppendLine("}" + suffix);
        }

        private void UpdateIndentation()
        {
            _indentation = GetIndentation(_level);
        }

        private static string GetIndentation(int level)
        {
            string indentation = string.Empty;

            for (int index = 0; index < level; index++)
            {
                indentation += Tab;
            }

            return indentation;
        }
    }
}