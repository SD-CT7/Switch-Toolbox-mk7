using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using Toolbox.Library;

namespace LayoutBXLYT
{
    public class BclytShader : BxlytShader
    {
        public BclytShader() : base()
        {
            LoadShaders();
        }
        
        public override void OnCompiled()
        {
            SetColor("whiteColor", Color.FromArgb(255, 255, 255, 255));
            SetColor("blackColor", Color.FromArgb(0, 0, 0, 0));
            SetInt("debugShading", 0);
            SetInt("hasTexture0", 0);
            SetInt("numTextureMaps", 0);
            SetInt("flipTexture", 0);
            SetInt("textures0", 0);
            SetInt("textures1", 0);
            SetInt("textures2", 0);
            SetBool("AlphaInterpolation", false);

            SetVec2("uvScale0", new Vector2(1, 1));
            SetFloat("uvRotate0", 0);
            SetVec2("uvTranslate0", new Vector2(0, 0));

            var rotationMatrix = Matrix4.Identity;
            SetMatrix("rotationMatrix", ref rotationMatrix);
            SetInt($"texCoords0GenType", 0);
            SetInt($"texCoords0Source", 0);
        }

        public static void SetMaterials(BxlytShader shader, CTR.Material material, BasePane pane, Dictionary<string, STGenericTexture> textures)
        {
            //Use the animated rotation (RotateX/Y/Z from BCLAN) if there is one
            var paneRotate = pane.GetRotation();
            Matrix4 rotationX = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(paneRotate.X));
            Matrix4 rotationY = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(paneRotate.Y));
            Matrix4 rotationZ = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(paneRotate.Z));
            var rotationMatrix = rotationX * rotationY * rotationZ;

            shader.SetMatrix("rotationMatrix", ref rotationMatrix);

            shader.SetColor("whiteColor", material.WhiteColor.Color);
            shader.SetColor("blackColor", material.BlackColor.Color);
            shader.SetInt("debugShading", (int)Runtime.LayoutEditor.Shading);
            shader.SetInt("numTextureMaps", material.TextureMaps.Length);
            shader.SetVec2("uvScale0", new Vector2(1, 1));
            shader.SetFloat("uvRotate0", 0);
            shader.SetVec2("uvTranslate0", new Vector2(0, 0));
            shader.SetInt("flipTexture", 0);
            shader.SetBool("AlphaInterpolation", material.AlphaInterpolation);
            shader.SetInt($"texCoords0GenType", 0);
            shader.SetInt($"texCoords0Source", 0);

            LoadTextureUniforms(shader, material, textures);
            LoadDefaultBlending();
        }

        public override string VertexShader
        {
            get
            {
                string path = System.IO.Path.Combine(Runtime.ExecutableDir, "Shader", "Layout", "Bflyt.vert");
                string legacyPath = System.IO.Path.Combine(Runtime.ExecutableDir, "Shader", "Layout", "Legacy", "Bflyt.vert");

                if (LayoutEditor.UseLegacyGL)
                    return System.IO.File.ReadAllText(legacyPath);
                else
                    return System.IO.File.ReadAllText(path);
            }
        }

        public override string FragmentShader
        {
            get
            {
                string path = System.IO.Path.Combine(Runtime.ExecutableDir, "Shader", "Layout", "Bflyt.frag");
                string legacyPath = System.IO.Path.Combine(Runtime.ExecutableDir, "Shader", "Layout", "Legacy", "Bflyt.frag");

                if (LayoutEditor.UseLegacyGL)
                    return System.IO.File.ReadAllText(legacyPath);
                else
                    return System.IO.File.ReadAllText(path);
            }
        }
    }

    //Builds a fragment shader from the TEV stages (combiners) of a 3DS (BCLYT) material.
    //Based on the TEV shader generation of Every File Explorer
    //(3DS/NintendoWare/LYT1/CLYTShader.cs) with a few fixes:
    // - the alpha combiners use floats
    // - the color/alpha scale (x1, x2, x4) is applied
    // - missing textures are treated as white like the regular layout shader
    //Not handled: "save previous register" flags and indirect/projection texture modes.
    public class BclytTevShader : BxlytShader
    {
        private static Dictionary<string, BclytTevShader> cache = new Dictionary<string, BclytTevShader>();

        private string fragmentSource;
        public bool IsValid = false;

        private BclytTevShader(string fragmentSource) : base()
        {
            this.fragmentSource = fragmentSource;
            LoadShaders();

            int linkStatus = 0;
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out linkStatus);
            IsValid = linkStatus == 1;
        }

        //Returns a shader for materials with TEV stages. Null if there are none or if the shader failed to build.
        public static BclytTevShader Get(CTR.Material material)
        {
            if (material == null || material.TevStages == null || material.TevStages.Length == 0)
                return null;

            string source = GenerateFragmentShader(material.TevStages);

            BclytTevShader shader;
            if (!cache.TryGetValue(source, out shader))
            {
                shader = new BclytTevShader(source);
                cache.Add(source, shader);
            }
            return shader.IsValid ? shader : null;
        }

        //Uniforms that the regular BclytShader.SetMaterials does not set
        public void SetTevUniforms(CTR.Material material)
        {
            //Buffer color ("Register" source) and the 6 constant colors
            SetColor("colorreg", material.BlackColor.Color);
            SetColor("const0", material.WhiteColor.Color);
            for (int i = 0; i < 5; i++)
            {
                Color constColor = Color.White;
                if (material.TevConstantColors != null && i < material.TevConstantColors.Length)
                    constColor = material.TevConstantColors[i].Color;
                SetColor("const" + (i + 1), constColor);
            }

            for (int i = 0; i < 3; i++)
            {
                //Default UVs as centered (same as the regular layout shader)
                Matrix4 matTransform = Matrix4.CreateTranslation(-0.5f, -0.5f, 0);

                if (i < material.TextureMaps.Length)
                {
                    var scale = new Syroot.Maths.Vector2F(1, 1);
                    float rotate = 0;
                    var translate = new Syroot.Maths.Vector2F(0, 0);

                    if (material.TextureTransforms != null && material.TextureTransforms.Length > i)
                    {
                        var transform = material.TextureTransforms[i];
                        scale = transform.Scale;
                        rotate = transform.Rotate;
                        translate = transform.Translate;

                        foreach (var animItem in material.animController.TextureSRTS)
                        {
                            switch (animItem.Key)
                            {
                                case LTSTarget.ScaleS: scale.X = animItem.Value; break;
                                case LTSTarget.ScaleT: scale.Y = animItem.Value; break;
                                case LTSTarget.Rotate: rotate = animItem.Value; break;
                                case LTSTarget.TranslateS: translate.X = animItem.Value; break;
                                case LTSTarget.TranslateT: translate.Y = animItem.Value; break;
                            }
                        }
                    }

                    var matScale = Matrix4.CreateScale(scale.X, scale.Y, 1.0f);
                    var matRotate = Matrix4.CreateFromAxisAngle(new Vector3(0, 0, 1), MathHelper.DegreesToRadians(rotate));
                    var matTranslate = Matrix4.CreateTranslation(
                        translate.X / scale.X - 0.5f,
                        translate.Y / scale.Y - 0.5f, 0);

                    //Center the UVs, scale, then rotate (same order as Every File Explorer),
                    //so the texture rotates around its center. The shader adds the 0.5 back afterwards.
                    matTransform = matTranslate * matScale * matRotate;
                }
                SetMatrix("textureTransform" + i, ref matTransform);

                //Which texture coordinate set this texture uses
                int source = 0;
                if (material.TexCoordGens != null && i < material.TexCoordGens.Length)
                {
                    source = (int)material.TexCoordGens[i].Source;
                    if (source > 2) source = 0; //projection modes are not supported
                }
                SetInt("texCoords" + i + "Source", source);
            }
        }

        public override string VertexShader
        {
            get
            {
                StringBuilder vert = new StringBuilder();
                vert.AppendLine("#version 130");
                vert.AppendLine("uniform mat4 rotationMatrix;");
                vert.AppendLine("uniform mat4 textureTransform0;");
                vert.AppendLine("uniform mat4 textureTransform1;");
                vert.AppendLine("uniform mat4 textureTransform2;");
                vert.AppendLine("uniform int texCoords0Source;");
                vert.AppendLine("uniform int texCoords1Source;");
                vert.AppendLine("uniform int texCoords2Source;");
                vert.AppendLine("vec4 getCoord(int source)");
                vert.AppendLine("{");
                vert.AppendLine("    if (source == 1) return gl_MultiTexCoord1;");
                vert.AppendLine("    if (source == 2) return gl_MultiTexCoord2;");
                vert.AppendLine("    return gl_MultiTexCoord0;");
                vert.AppendLine("}");
                vert.AppendLine("void main()");
                vert.AppendLine("{");
                vert.AppendLine("    gl_FrontColor = gl_Color;");
                vert.AppendLine("    gl_TexCoord[0] = textureTransform0 * getCoord(texCoords0Source);");
                vert.AppendLine("    gl_TexCoord[0].st = vec2(0.5, 0.5) + gl_TexCoord[0].st;");
                vert.AppendLine("    gl_TexCoord[1] = textureTransform1 * getCoord(texCoords1Source);");
                vert.AppendLine("    gl_TexCoord[1].st = vec2(0.5, 0.5) + gl_TexCoord[1].st;");
                vert.AppendLine("    gl_TexCoord[2] = textureTransform2 * getCoord(texCoords2Source);");
                vert.AppendLine("    gl_TexCoord[2].st = vec2(0.5, 0.5) + gl_TexCoord[2].st;");
                vert.AppendLine("    gl_Position = gl_ModelViewProjectionMatrix * rotationMatrix * gl_Vertex;");
                vert.AppendLine("}");
                return vert.ToString();
            }
        }

        public override string FragmentShader
        {
            get { return fragmentSource; }
        }

        //BEGIN GENERATOR
        //Indexed by TevSource: Tex0, Tex1, Tex2, Tex3, Constant, Primary, Previous, Register
        private static readonly string[] SourceCode = new string[]
        {
            "getTex0()", "getTex1()", "getTex2()", "vec4(1.0)", "{0}", "gl_Color", "previous", "colorreg"
        };

        //Indexed by TevColorOp
        private static readonly string[] ColorOpCode = new string[]
        {
            "{0}.rgb", "vec3(1.0) - {0}.rgb", "{0}.aaa", "vec3(1.0) - {0}.aaa",
            "{0}.rrr", "vec3(1.0) - {0}.rrr", "{0}.ggg", "vec3(1.0) - {0}.ggg",
            "{0}.bbb", "vec3(1.0) - {0}.bbb"
        };

        //Indexed by TevAlphaOp
        private static readonly string[] AlphaOpCode = new string[]
        {
            "{0}.a", "(1.0 - {0}.a)", "{0}.r", "(1.0 - {0}.r)",
            "{0}.g", "(1.0 - {0}.g)", "{0}.b", "(1.0 - {0}.b)"
        };

        //Indexed by TevMode
        private static readonly string[] ColorCombinerCode = new string[]
        {
            "j1.rgb",
            "j1.rgb * j2.rgb",
            "j1.rgb + j2.rgb",
            "j1.rgb + j2.rgb - vec3(0.5)",
            "j1.rgb * j3.rgb + j2.rgb * (vec3(1.0) - j3.rgb)",
            "j1.rgb - j2.rgb",
            "clamp(j1.rgb + j2.rgb, 0.0, 1.0) * j3.rgb",
            "(j1.rgb * j2.rgb) + j3.rgb",
            "j1.rgb", "j1.rgb", "j1.rgb", "j1.rgb"
        };

        private static readonly string[] AlphaCombinerCode = new string[]
        {
            "j1.a",
            "j1.a * j2.a",
            "j1.a + j2.a",
            "j1.a + j2.a - 0.5",
            "j1.a * j3.a + j2.a * (1.0 - j3.a)",
            "j1.a - j2.a",
            "clamp(j1.a + j2.a, 0.0, 1.0) * j3.a",
            "(j1.a * j2.a) + j3.a",
            "j1.a", "j1.a", "j1.a", "j1.a"
        };

        private static readonly string[] ScaleCode = new string[] { "1.0", "2.0", "4.0" };

        private static string Pick(string[] table, int index)
        {
            if (index < 0 || index >= table.Length)
                index = 0;
            return table[index];
        }

        public static string GenerateFragmentShader(BxlytTevStage[] stages)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("#version 130");
            for (int i = 0; i < 3; i++)
            {
                sb.AppendLine("uniform sampler2D textures" + i + ";");
                sb.AppendLine("uniform int hasTexture" + i + ";");
            }
            sb.AppendLine("uniform vec4 colorreg;");
            for (int i = 0; i < 6; i++)
                sb.AppendLine("uniform vec4 const" + i + ";");

            //Missing textures are white (same as the regular layout shader)
            for (int i = 0; i < 3; i++)
            {
                sb.AppendLine("vec4 getTex" + i + "()");
                sb.AppendLine("{");
                sb.AppendLine("    if (hasTexture" + i + " == 1)");
                sb.AppendLine("        return texture2D(textures" + i + ", gl_TexCoord[" + i + "].st);");
                sb.AppendLine("    return vec4(1.0);");
                sb.AppendLine("}");
            }

            sb.AppendLine("void main()");
            sb.AppendLine("{");
            sb.AppendLine("    vec4 j1;");
            sb.AppendLine("    vec4 j2;");
            sb.AppendLine("    vec4 j3;");
            sb.AppendLine("    vec4 previous = gl_Color;");

            foreach (var baseStage in stages)
            {
                var stage = baseStage as CTR.TevStage;
                if (stage == null || stage.ColorSources == null || stage.ColorOperators == null ||
                    stage.AlphaSources == null || stage.AlphaOperators == null)
                    continue;

                //Which of the 6 constant colors this stage uses (1 - 6, 0 = none)
                int colorConst = (int)(stage.ConstColors & 0xF) - 1;
                int alphaConst = (int)((stage.ConstColors >> 4) & 0xF) - 1;
                if (colorConst < 0 || colorConst > 5) colorConst = 0;
                if (alphaConst < 0 || alphaConst > 5) alphaConst = 0;

                for (int j = 0; j < 3; j++)
                {
                    string colorSource = string.Format(Pick(SourceCode, (int)stage.ColorSources[j]), "const" + colorConst);
                    string alphaSource = string.Format(Pick(SourceCode, (int)stage.AlphaSources[j]), "const" + alphaConst);
                    string color = string.Format(Pick(ColorOpCode, (int)stage.ColorOperators[j]), colorSource);
                    string alpha = string.Format(Pick(AlphaOpCode, (int)stage.AlphaOperators[j]), alphaSource);

                    sb.AppendLine("    j" + (j + 1) + " = vec4(" + color + ", " + alpha + ");");
                }

                string rgb = Pick(ColorCombinerCode, (int)stage.ColorMode);
                string a = Pick(AlphaCombinerCode, (int)stage.AlphaMode);
                string rgbScale = Pick(ScaleCode, (int)stage.ColorScale);
                string aScale = Pick(ScaleCode, (int)stage.AlphaScale);

                sb.AppendLine("    previous = vec4(clamp((" + rgb + ") * " + rgbScale + ", 0.0, 1.0), " +
                              "clamp((" + a + ") * " + aScale + ", 0.0, 1.0));");
            }

            sb.AppendLine("    gl_FragColor = previous;");
            sb.AppendLine("}");
            return sb.ToString();
        }
        //END GENERATOR
    }
}
