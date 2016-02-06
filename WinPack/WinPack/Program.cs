using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Xml.Linq;

namespace WinPack
{
    class Program
    {
        static BinaryWriter bwrite;
        static BinaryReader bread;
        static uint form_size;
        static uint FONT_offset;
        static ushort TXTR_count;
        static bool translatale;
        static List<fontInfo> newFonts = new List<fontInfo>();
        static string[] chunks = new string[] { "GEN8","OPTN","EXTN","SOND","AGRP","SPRT","BGND","PATH","SCPT","SHDR","FONT","TMLN","OBJT","ROOM","DAFL","TPAG","CODE","VARI",
                                    "FUNC","STRG","TXTR","AUDO" };

        struct fontInfo {
            public int id;
            public string filename;
            public uint font_offset;
            public uint filename_offset;
            public uint fontname_offset;
            public uint image_offset;
            public uint glyph_count;
            public XDocument data;
        }
        
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                System.Console.WriteLine("Usage: winpack <output folder> <input .win file>");
                return;
            }
            string output_folder = args[0];
            if (output_folder[output_folder.Length-1]!='\\') output_folder += '\\';
            string input_win = args[1];
            if (args.Length >= 3) translatale = (args[2] == "-tt");
            form_size = 0;

            string patch_path0 = output_folder + "FONT_new\\";
            if (File.Exists(patch_path0 + "patch.txt"))
            {
                string[] patchLines = File.ReadAllLines(patch_path0 + "patch.txt", System.Text.Encoding.UTF8);
                for (int l=0; l<patchLines.Length; l++)
                {
                    if (patchLines[l].IndexOf("//") == 0) continue;
                    string[] par = patchLines[l].Split(';');
                    fontInfo newFont = new fontInfo();
                    newFont.id = Convert.ToInt32(par[0]);
                    newFont.filename = par[1].Substring(0, par[1].Length - ".gmx".Length);
                    newFont.data = XDocument.Load(patch_path0+par[1]);
                    newFonts.Add(newFont);                    
                }
            }

            TXTR_count = (ushort)Directory.GetFiles(output_folder + "TXTR").Length;

            bwrite = new BinaryWriter(File.Open(input_win, FileMode.Create));
            bwrite.Write(System.Text.Encoding.ASCII.GetBytes("FORM"));
            bwrite.Write(form_size);

            for (int i=0; i<chunks.Length; i++)
            {
                string chunk_name = chunks[i];
                uint chunk_size = 0;
                bwrite.Write(System.Text.Encoding.ASCII.GetBytes(chunk_name));                
                uint chunk_offset = (uint)bwrite.BaseStream.Position;                
                bwrite.Write(chunk_size);
                form_size += 8;                
                                
                if (chunk_name == "STRG")
                {                    
                    string[] strg = File.ReadAllLines(output_folder + "STRG.txt", System.Text.Encoding.UTF8);
                    uint lines = (uint)strg.Length;
                    if (strg[(int)(lines - 1)].Length == 0) lines--;
                                        
                    bwrite.Write(lines);
                    chunk_size += 4;

                    uint[] Offsets = new uint[lines];
                    //Lines offsets
                    for (int f = 0; f < lines; f++)
                    {
                        Offsets[f] = (uint)bwrite.BaseStream.Position;
                        bwrite.Write((uint)0);
                        chunk_size += 4;
                    }
                    //Lines (line size + line + 0)
                    for (int f = 0; f < lines; f++)
                    {
                        uint line_off = (uint)bwrite.BaseStream.Position;
                        bwrite.BaseStream.Position = Offsets[f];
                        bwrite.Write(line_off);
                        bwrite.BaseStream.Position = line_off;

                        string oneLine = strg[f];
                        uint lineLen = (uint)oneLine.Length;
                        bwrite.Write(lineLen); chunk_size += 4;
                        for (int j = 0; j < lineLen; j++)
                            bwrite.Write(oneLine[j]); 
                        chunk_size += (uint)System.Text.Encoding.UTF8.GetByteCount(oneLine);
                        bwrite.Write((byte)0); chunk_size += 1;
                    }

                    //Edited lines                    
                    if (File.Exists(output_folder + "translate.txt"))
                    {
                        string[] patch = File.ReadAllLines(output_folder + "translate.txt", System.Text.Encoding.UTF8);
                        lines = (uint)patch.Length;
                        if (lines != strg.Length) System.Console.WriteLine("Warning: STRG.txt has "+ strg.Length + " lines, translate.txt has "+lines+" lines");
                        if (lines>0 && patch[(int)(lines - 1)].Length == 0) lines--;

                        if (translatale) {
                            for (int f = 0; f < lines; f++)
                            {
                                string oneLine = patch[f];
                                if (strg[f] == oneLine) continue;

                                uint lineN = (uint)(f+1);
                                uint line_offset = chunk_offset + (lineN + 1) * 4;

                                uint line_off = (uint)bwrite.BaseStream.Position;
                                bwrite.BaseStream.Position = line_offset;
                                bwrite.Write(line_off);
                                bwrite.BaseStream.Position = line_off;

                                uint lineLen = (uint)oneLine.Length;
                                bwrite.Write(lineLen); chunk_size += 4;
                                for (int j = 0; j < lineLen; j++)
                                    bwrite.Write(oneLine[j]);
                                chunk_size += (uint)System.Text.Encoding.UTF8.GetByteCount(oneLine);
                                bwrite.Write((byte)0); chunk_size += 1;
                            }
                        } else {
                            bool patchNumber = true;
                            for (int f = 0; f < lines; f++)
                            {
                                string oneLine = patch[f];
                                if (oneLine.IndexOf("//") == 0) continue;

                                if (patchNumber)
                                {
                                    uint lineN = System.Convert.ToUInt32(oneLine);
                                    uint line_offset = chunk_offset + (lineN + 1) * 4;

                                    uint line_off = (uint)bwrite.BaseStream.Position;
                                    bwrite.BaseStream.Position = line_offset;
                                    bwrite.Write(line_off);
                                    bwrite.BaseStream.Position = line_off;
                                }
                                else {
                                    uint lineLen = (uint)oneLine.Length;
                                    bwrite.Write(lineLen); chunk_size += 4;
                                    for (int j = 0; j < lineLen; j++)
                                        bwrite.Write(oneLine[j]);
                                    chunk_size += (uint)System.Text.Encoding.UTF8.GetByteCount(oneLine);
                                    bwrite.Write((byte)0); chunk_size += 1;
                                }
                                patchNumber = !patchNumber;
                            }
                        }
                    }
                    else {
                        System.Console.WriteLine("translate.txt not found. Strings will not be modified.");
                    }
                    //Font strings
                    for (int f0 = 0; f0 < newFonts.Count; f0++)
                    {
                        fontInfo fi = newFonts[f0];
                                                
                        string font_name = fi.filename;
                        uint lineLen = (uint)font_name.Length;
                        bwrite.Write(lineLen); chunk_size += 4;
                        fi.filename_offset = (uint)bwrite.BaseStream.Position;
                        for (int j = 0; j < lineLen; j++)
                            bwrite.Write(font_name[j]);
                        chunk_size += (uint)System.Text.Encoding.UTF8.GetByteCount(font_name);
                        bwrite.Write((byte)0); chunk_size += 1;
                                                
                        font_name = fi.data.Element("font").Element("name").Value;
                        lineLen = (uint)font_name.Length;
                        bwrite.Write(lineLen); chunk_size += 4;
                        fi.fontname_offset = (uint)bwrite.BaseStream.Position;
                        for (int j = 0; j < lineLen; j++)
                            bwrite.Write(font_name[j]);
                        chunk_size += (uint)System.Text.Encoding.UTF8.GetByteCount(font_name);
                        bwrite.Write((byte)0); chunk_size += 1;

                        newFonts[f0] = fi;
                    }

                    //Fonts                    
                    for (ushort f0 = 0; f0 < newFonts.Count; f0++)
                    {
                        fontInfo fi = newFonts[f0];
                                                
                        string font_name = fi.filename;
                        uint lineLen = (uint)font_name.Length;
                        bwrite.Write(lineLen); chunk_size += 4;
                        fi.filename_offset = (uint)bwrite.BaseStream.Position;
                        for (int j = 0; j < lineLen; j++)
                            bwrite.Write(font_name[j]);
                        chunk_size += (uint)System.Text.Encoding.UTF8.GetByteCount(font_name);
                        bwrite.Write((byte)0); chunk_size += 1;
                                                
                        font_name = fi.data.Element("font").Element("name").Value;
                        lineLen = (uint)font_name.Length;
                        bwrite.Write(lineLen); chunk_size += 4;
                        fi.fontname_offset = (uint)bwrite.BaseStream.Position;
                        for (int j = 0; j < lineLen; j++)
                            bwrite.Write(font_name[j]);
                        chunk_size += (uint)System.Text.Encoding.UTF8.GetByteCount(font_name);
                        bwrite.Write((byte)0); chunk_size += 1;

                        newFonts[f0] = fi;
                    }

                    //Fonts                    
                    for (ushort f0 = 0; f0 < newFonts.Count; f0++)
                    {
                        fontInfo fi = newFonts[f0];

                        uint bacp = (uint)bwrite.BaseStream.Position;
                        bwrite.BaseStream.Position = FONT_offset + 4 * (fi.id + 1);//!!!
                        bwrite.Write(bacp);
                        bwrite.BaseStream.Position = bacp;
                        
                        fi = recordNewFont(fi);
                        chunk_size += calculateFontSize(fi.glyph_count);

                        newFonts[f0] = fi;
                    }

                    //Sprites
                    for (ushort f0 = 0; f0 < newFonts.Count; f0++)
                    {
                        fontInfo fi = newFonts[f0];

                        long bacp = bwrite.BaseStream.Position;
                        bwrite.BaseStream.Position = fi.image_offset;//!!!
                        bwrite.Write((uint)bacp);
                        bwrite.BaseStream.Position = bacp;

                        ushort w = 0;
                        ushort h = 0;
                        string imagePath = output_folder + "FONT_new\\" + fi.data.Element("font").Element("image").Value;
                        using(FileStream stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                        {
                            Image texture = Image.FromStream(stream);
                            w = (ushort)texture.Width;
                            h = (ushort)texture.Height;
                        }
                        ushort x = 0;
                        ushort y = 0;                        
                        ushort s = (ushort)(TXTR_count + f0 + 1);//???

                        editSprite((uint)bwrite.BaseStream.Position, x, y, w, h, s);                        
                        bwrite.BaseStream.Position += 22;
                        chunk_size += 22;
                    }

                    bwrite.Write((uint)0); chunk_size += 4;
                }
                else if (chunk_name == "TXTR")
                {
                    uint files = TXTR_count;
                    bwrite.Write((uint)(files + newFonts.Count));
                    chunk_size += 4;

                    uint[] Offsets = new uint[files+newFonts.Count];
                    
                    //Headers offset
                    for (int f = 0; f < files; f++)
                    {
                        Offsets[f] = (uint)bwrite.BaseStream.Position;
                        bwrite.Write((uint)0);
                        chunk_size += 4;
                    }
                    for (int f = 0; f < newFonts.Count; f++)
                    {
                        Offsets[f+files] = (uint)bwrite.BaseStream.Position;
                        bwrite.Write((uint)0);
                        chunk_size += 4;
                    }

                    //Headers
                    for (int f = 0; f < files; f++)
                    {
                        uint header_off = (uint)bwrite.BaseStream.Position;
                        bwrite.BaseStream.Position = Offsets[f];
                        bwrite.Write(header_off);
                        bwrite.BaseStream.Position = header_off;

                        bwrite.Write((uint)1);
                        Offsets[f] = (uint)bwrite.BaseStream.Position;
                        bwrite.Write((uint)0);
                        chunk_size += 8;
                    }
                    for (int f = 0; f < newFonts.Count; f++)
                    {
                        uint header_off = (uint)bwrite.BaseStream.Position;
                        bwrite.BaseStream.Position = Offsets[f+files];
                        bwrite.Write(header_off);
                        bwrite.BaseStream.Position = header_off;

                        bwrite.Write((uint)1);
                        Offsets[f + files] = (uint)bwrite.BaseStream.Position;
                        bwrite.Write((uint)0);
                        chunk_size += 8;
                    }

                    //Неизвестно, зачем здесь нули, но игра запускается и без них
                    //Если требуется сравнить оригинальный win со сгенерированным, расскоментируйте строки
                    //for (int f=0; f<13; f++) 
                    //{
                    //    bwrite.Write((uint)0); chunk_size += 4;
                    //}

                    //Files
                    for (int f0 = 0; f0 < files; f0++)
                    {
                        uint file_off = (uint)bwrite.BaseStream.Position;
                        bwrite.BaseStream.Position = Offsets[f0];
                        bwrite.Write(file_off);
                        bwrite.BaseStream.Position = file_off;

                        uint file_size = (uint)new FileInfo(output_folder + chunk_name + "\\" + f0 + ".png").Length;
                        bread = new BinaryReader(File.Open (output_folder + chunk_name + "\\" + f0 + ".png", FileMode.Open));                        
                        for (uint j = 0; j < file_size; j++)
                            bwrite.Write(bread.ReadByte());
                        chunk_size += file_size;
                    }
                    for (int f0 = 0; f0 < newFonts.Count; f0++)
                    {
                        uint file_off = (uint)bwrite.BaseStream.Position;
                        bwrite.BaseStream.Position = Offsets[f0+files];
                        bwrite.Write(file_off);
                        bwrite.BaseStream.Position = file_off;

                        string imagePath = output_folder + "FONT_new\\" + newFonts[f0].data.Element("font").Element("image").Value;

                        uint file_size = (uint)new FileInfo(imagePath).Length;
                        bread = new BinaryReader(File.Open(imagePath, FileMode.Open));
                        for (uint j = 0; j < file_size; j++)
                            bwrite.Write(bread.ReadByte());
                        chunk_size += file_size;
                    }
                }
                else if (chunk_name == "AUDO")
                {
                    uint files = (uint)Directory.GetFiles(output_folder + chunk_name).Length;
                    bwrite.Write(files); chunk_size += 4;

                    uint[] Offsets = new uint[files];

                    //Headers offset
                    for (int f = 0; f < files; f++)
                    {
                        Offsets[f] = (uint)bwrite.BaseStream.Position;
                        bwrite.Write((uint)0);
                        chunk_size += 4;
                    }
                    
                    for (uint f0=0; f0< files; f0++)
                    {                        
                        uint file_off = (uint)bwrite.BaseStream.Position;
                        bwrite.BaseStream.Position = Offsets[f0];
                        bwrite.Write(file_off);
                        bwrite.BaseStream.Position = file_off;

                        uint file_size = (uint)new FileInfo(output_folder + chunk_name + "\\" + f0 + ".wav").Length;
                        bwrite.Write(file_size); chunk_size += 4;
                        bread = new BinaryReader(File.Open (output_folder + chunk_name + "\\" + f0 + ".wav", FileMode.Open));
                        for (uint j = 0; j < file_size; j++)
                            bwrite.Write(bread.ReadByte());
                        chunk_size += file_size;
                        if (f0 == files - 1) continue;
                        for (int j = 0; j < file_size % 4; j++)
                        { bwrite.Write((byte)0); chunk_size++; }                            
                    }
                }
                else
                {
                    string filer = output_folder + "CHUNK\\" +chunk_name + ".chunk";
                    if (chunk_name == "FONT") FONT_offset = (uint)bwrite.BaseStream.Position;
                    chunk_size = (uint)new FileInfo(filer).Length;
                    bread = new BinaryReader(File.Open(filer, FileMode.Open));                    
                    for (uint j = 0; j < chunk_size; j++)
                        bwrite.Write(bread.ReadByte());                    
                }
                                
                uint chunk_end = (uint)bwrite.BaseStream.Position;
                bwrite.BaseStream.Position = chunk_offset;
                bwrite.Write(chunk_size);
                bwrite.BaseStream.Position = chunk_end;
                form_size += chunk_size;
                
                System.Console.WriteLine("Chunk " + chunk_name + " offset:" + (chunk_offset-4) + " size:" + (chunk_size+8));
            }            

            bwrite.BaseStream.Position = 4;
            bwrite.Write(form_size);

            //bwrite.BaseStream.Position = 0x725D8C;//Debug mode
            //bwrite.Write((byte)1);
        }

        static void editSprite(uint sprite_offset, ushort x, ushort y, ushort w, ushort h, ushort s) {
            long bacp = bwrite.BaseStream.Position;
            bwrite.BaseStream.Position = sprite_offset;
            bwrite.Write(x);
            bwrite.Write(y);
            bwrite.Write(w);
            bwrite.Write(h);
            bwrite.Write((uint)0);
            bwrite.Write(w);
            bwrite.Write(h);
            bwrite.Write(w);
            bwrite.Write(h);
            bwrite.Write(s);
            bwrite.BaseStream.Position = bacp;
        } 
        
        static fontInfo recordNewFont(fontInfo font)
        {
            font.font_offset = (uint)bwrite.BaseStream.Position;
            bwrite.Write(font.filename_offset);
            bwrite.Write(font.fontname_offset);
            bwrite.Write(Convert.ToUInt32(font.data.Element("font").Element("size").Value));
            bwrite.Write(font.data.Element("font").Element("bold").Value == "0" ? (uint)0 : (uint)1);
            bwrite.Write(font.data.Element("font").Element("italic").Value == "0" ? (uint)0 : (uint)1);
            string[] range0 = font.data.Element("font").Element("ranges").Element("range0").Value.Split(',');
            bwrite.Write(Convert.ToUInt16(range0[0]));
            bwrite.Write((ushort)1);//?
            bwrite.Write(Convert.ToUInt16(range0[1]));
            bwrite.Write((ushort)0);//?
            font.image_offset = (uint)bwrite.BaseStream.Position;
            bwrite.Write((uint)0);//sprite
            bwrite.Write((uint)0x3F800000);//?
            bwrite.Write((uint)0x3F800000);//?
                        
            IEnumerable<XElement> glyphs = font.data.Element("font").Element("glyphs").Elements("glyph");
            uint glyphCount = 0;
            foreach (XElement glyph in glyphs) glyphCount++;
            font.glyph_count = glyphCount;
            uint[] Offsets = new uint[glyphCount];
            bwrite.Write(glyphCount);
            for (int g=0; g<glyphCount; g++)
            {
                Offsets[g] = (uint)bwrite.BaseStream.Position;
                bwrite.Write((uint)0);
            }
            int f0 = 0;
            foreach (XElement glyph in glyphs)
            {
                uint file_off = (uint)bwrite.BaseStream.Position;
                bwrite.BaseStream.Position = Offsets[f0];
                bwrite.Write(file_off);
                bwrite.BaseStream.Position = file_off;

                bwrite.Write(Convert.ToUInt16(glyph.Attribute("character").Value));
                bwrite.Write(Convert.ToUInt16(glyph.Attribute("x").Value));
                bwrite.Write(Convert.ToUInt16(glyph.Attribute("y").Value));
                bwrite.Write(Convert.ToUInt16(glyph.Attribute("w").Value));
                bwrite.Write(Convert.ToUInt16(glyph.Attribute("h").Value));
                bwrite.Write(Convert.ToUInt16(glyph.Attribute("shift").Value));
                bwrite.Write(Convert.ToInt16(glyph.Attribute("offset").Value));//sic!
                bwrite.Write((ushort)0);

                f0++;
            }
            return font;
        }

        static uint calculateFontSize(uint glyph_count)
        {           
            return 44 + glyph_count * 20;
        }
    }
}
