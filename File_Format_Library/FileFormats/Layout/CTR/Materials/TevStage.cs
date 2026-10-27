using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Library.IO;

namespace LayoutBXLYT.CTR
{
    public class TevStage : BxlytTevStage
    {
        public TevSource[] ColorSources;
        public TevColorOp[] ColorOperators;
        public TevScale ColorScale;
        public Boolean ColorSavePrevReg;

        public byte ColorUnknown;
        public TevSource[] AlphaSources;
        public TevAlphaOp[] AlphaOperators;
        public TevScale AlphaScale;
        public Boolean AlphaSavePrevReg;

        public uint ConstColors;

        private uint flags1;
        private uint flags2;

        public TevStage() { }

        public TevStage(FileReader reader, BxlytHeader header)
        {
            flags1 = reader.ReadUInt32();
            ColorSources = new TevSource[] { (TevSource)(flags1 & 0xF), (TevSource)((flags1 >> 4) & 0xF), (TevSource)((flags1 >> 8) & 0xF) };
            ColorOperators = new TevColorOp[] { (TevColorOp)((flags1 >> 12) & 0xF), (TevColorOp)((flags1 >> 16) & 0xF), (TevColorOp)((flags1 >> 20) & 0xF) };
            ColorMode = (TevMode)((flags1 >> 24) & 0xF);
            ColorScale = (TevScale)((flags1 >> 28) & 0x3);
            ColorSavePrevReg = ((flags1 >> 30) & 0x1) == 1;
            flags2 = reader.ReadUInt32();
            AlphaSources = new TevSource[] { (TevSource)(flags2 & 0xF), (TevSource)((flags2 >> 4) & 0xF), (TevSource)((flags2 >> 8) & 0xF) };
            AlphaOperators = new TevAlphaOp[] { (TevAlphaOp)((flags2 >> 12) & 0xF), (TevAlphaOp)((flags2 >> 16) & 0xF), (TevAlphaOp)((flags2 >> 20) & 0xF) };
            AlphaMode = (TevMode)((flags2 >> 24) & 0xF);
            AlphaScale = (TevScale)((flags2 >> 28) & 0x3);
            AlphaSavePrevReg = ((flags2 >> 30) & 0x1) == 1;

            ConstColors = reader.ReadUInt32();
        }

        public void Write(FileWriter writer)
        {
            //Rebuild the flags from the properties so changes made in the combiner editor are saved.
            //Bit 31 is unknown so the original value is kept.
            if (ColorSources != null && ColorOperators != null && AlphaSources != null && AlphaOperators != null &&
                ColorSources.Length == 3 && ColorOperators.Length == 3 && AlphaSources.Length == 3 && AlphaOperators.Length == 3)
            {
                flags1 = (flags1 & 0x80000000) |
                    ((uint)ColorSources[0] & 0xF) | (((uint)ColorSources[1] & 0xF) << 4) | (((uint)ColorSources[2] & 0xF) << 8) |
                    (((uint)ColorOperators[0] & 0xF) << 12) | (((uint)ColorOperators[1] & 0xF) << 16) | (((uint)ColorOperators[2] & 0xF) << 20) |
                    (((uint)ColorMode & 0xF) << 24) | (((uint)ColorScale & 0x3) << 28) | ((ColorSavePrevReg ? 1u : 0u) << 30);

                flags2 = (flags2 & 0x80000000) |
                    ((uint)AlphaSources[0] & 0xF) | (((uint)AlphaSources[1] & 0xF) << 4) | (((uint)AlphaSources[2] & 0xF) << 8) |
                    (((uint)AlphaOperators[0] & 0xF) << 12) | (((uint)AlphaOperators[1] & 0xF) << 16) | (((uint)AlphaOperators[2] & 0xF) << 20) |
                    (((uint)AlphaMode & 0xF) << 24) | (((uint)AlphaScale & 0x3) << 28) | ((AlphaSavePrevReg ? 1u : 0u) << 30);
            }

            writer.Write(flags1);
            writer.Write(flags2);
            writer.Write(ConstColors); //the reader consumes 12 bytes per TEV stage
        }
    }
}
