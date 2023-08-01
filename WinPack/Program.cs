﻿using System;
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
        static uint STRG_offset;
        static ushort TXTR_count;
        static bool translatale;
        static bool strgWithBr = false;
        static bool correctTXTR;
        static bool noAUDO;
        static bool UTswitch;
        static List<fontInfo> newFonts = new List<fontInfo>();
        static string[] chunks = new string[] { "GEN8","OPTN","LANG","EXTN","SOND","AGRP","SPRT","BGND","PATH","SCPT","GLOB","SHDR","FONT",
                                                "TMLN","OBJT","ROOM","DAFL","EMBI","TPAG","CODE","VARI","FUNC","STRG","TXTR","AUDO"};

        struct fontInfo {
            public int id;
            public string filename;
            public uint font_offset;
            public uint filename_offset;
            public uint fontname_offset;
            public uint image_offset;
            public uint glyph_count;
            public uint txtr_id;
            public uint txtr_x;
            public uint txtr_y;
            public XDocument data;
        }
        
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                System.Console.WriteLine("Usage: winpack <output folder> <input .win file> -noAUDO (When the folder AUDO doesn't exist and is a chunk)");
                return;
            }
            string output_folder = args[0];
            if (output_folder[output_folder.Length-1]!='\\') output_folder += '\\';
            string input_win = args[1];
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-tt") translatale = true;
                if (args[i] == "-correctTXTR") correctTXTR = true;
                if (args[i] == "-noAUDO") noAUDO = true;
                if (args[i] == "-switch") UTswitch = true;
                if (args[i] == "-strgWithBr") strgWithBr = true;
            }
            translatale = true;
            
            bool useTXTR = Directory.Exists(output_folder + "TXTR");
            form_size = 0;

            uint chunk_end = 0;
            uint chunk_size = 0;
            uint chunk_offset = 0;

            string ext = strgWithBr ? "strg" : "txt";
            List<string> strg = new List<string>();
            uint lines = 0;
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
                    newFont.txtr_id = Convert.ToUInt32(par[2]);
                    newFont.txtr_x = Convert.ToUInt32(par[3]);
                    newFont.txtr_y = Convert.ToUInt32(par[4]);
                    newFonts.Add(newFont);
                }
            }
            
            TXTR_count = useTXTR ? (ushort)Directory.GetFiles(output_folder + "TXTR").Length : (ushort)0;
            
            bwrite = new BinaryWriter(File.Open(input_win, FileMode.Create));
            bwrite.Write(System.Text.Encoding.ASCII.GetBytes("FORM"));
            bwrite.Write(form_size);

            for (int i=0; i<chunks.Length; i++)
            {
                
                string chunk_name = chunks[i];

                if (!File.Exists(output_folder + "CHUNK\\" + chunk_name + ".chunk"))
                    if (!(chunk_name == "AUDO" || (chunk_name == "TXTR" && useTXTR)))
                        continue;

                chunk_size = 0;
                bwrite.Write(System.Text.Encoding.ASCII.GetBytes(chunk_name));
                chunk_offset = (uint)bwrite.BaseStream.Position;
                bwrite.Write(chunk_size);
                form_size += 8;
                
                if (chunk_name == "STRG")
                {
                    STRG_offset = chunk_offset;
                    
                    strg = createStrgList(output_folder + "original."+ext);
                    //string[] strg = File.ReadAllLines(output_folder + "original.strg", System.Text.Encoding.UTF8);
                    lines = (uint)strg.Count;
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
                        uint byteLen = (uint)System.Text.Encoding.UTF8.GetByteCount(oneLine);
                        bwrite.Write(byteLen); chunk_size += 4;
                        for (int j = 0; j < lineLen; j++)
                            bwrite.Write(oneLine[j]);
                        chunk_size += byteLen;
                        bwrite.Write((byte)0); chunk_size += 1;
                    }

                    //Modified info was here. Replaced into end of FORM

                    bwrite.Write((uint)0); chunk_size += 4;
                    //Unknown purpose zeros
                    for (int f=0; f<0x25; f++) 
                    {
                        bwrite.Write((byte)0); chunk_size += 1;
                    }
                }
                else if (chunk_name == "TXTR")
                {
                    uint files = TXTR_count;
                    uint filesf = (uint)(files + newFonts.Count);
                    bwrite.Write(files);
                    chunk_size += 4;

                    uint[] Offsets = new uint[files];
                    
                    //Headers offset
                    for (int f = 0; f < files; f++)
                    {
                        Offsets[f] = (uint)bwrite.BaseStream.Position;
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

                        if (correctTXTR)
                        {
                            bwrite.Write(0x00000000);
                            bwrite.Write(0xFFFFFFFF);
                        } else bwrite.Write((uint)0);//1?
                        if (UTswitch) { 
                            bwrite.Write((uint)0);//Switch
                            chunk_size += 4;//Switch
                        }
                        Offsets[f] = (uint)bwrite.BaseStream.Position;
                        bwrite.Write((uint)0);
                        chunk_size += 8; if (correctTXTR) chunk_size += 4;
                    }
                    
                    //Неизвестно, зачем здесь нули, но игра запускается и без них
                    //Если требуется сравнить оригинальный win со сгенерированным, расскоментируйте строки
                    for (int f=0; f<0x74; f++) 
                    {
                        bwrite.Write((byte)0); chunk_size += 1;
                    }

                    //Files
                    for (int f0 = 0; f0 < files; f0++)
                    {
                        bool modified = false;
                        uint file_off = (uint)bwrite.BaseStream.Position;
                        bwrite.BaseStream.Position = Offsets[f0];
                        bwrite.Write(file_off);
                        bwrite.BaseStream.Position = file_off;

                        //Modify TXTR with new font
                        Bitmap texture;
                        using (var tempbmp = new Bitmap(output_folder + chunk_name + "\\" + f0 + ".png"))
                        {
                            texture = new Bitmap(tempbmp);
                            Graphics newtext = Graphics.FromImage(texture);
                            for (int ff = 0; ff < newFonts.Count; ff++)
                            {
                                fontInfo fi = newFonts[ff];
                                if (fi.txtr_id == f0)
                                {
                                    modified = true;
                                    string imagePath = output_folder + "FONT_new\\" + fi.data.Element("font").Element("image").Value;
                                    Bitmap fontImage = new Bitmap(imagePath);
                                    newtext.SetClip(new Rectangle((int)fi.txtr_x, (int)fi.txtr_y, fontImage.Width, fontImage.Height));
                                    newtext.Clear(Color.Transparent);
                                    newtext.DrawImage(fontImage, fi.txtr_x, fi.txtr_y);
                                }
                            }
                            if (modified) texture.Save(output_folder + "FONT_new\\" + f0 + "m.png");//?
                        }
                        
                        string txtr_path = modified ? output_folder + "FONT_new\\" + f0 + "m.png" : output_folder + chunk_name + "\\" + f0 + ".png";

                        uint file_size = (uint)new FileInfo(txtr_path).Length;
                        bread = new BinaryReader(File.Open (txtr_path, FileMode.Open));
                        for (uint j = 0; j < file_size; j++)
                            bwrite.Write(bread.ReadByte());
                        chunk_size += file_size;
                    }
                    //for (int f0 = 0; f0 < newFonts.Count; f0++)
                    //{
                    //    uint file_off = (uint)bwrite.BaseStream.Position;
                    //    bwrite.BaseStream.Position = Offsets[f0+files];
                    //    bwrite.Write(file_off);
                    //    bwrite.BaseStream.Position = file_off;
                    //
                    //    string imagePath = output_folder + "FONT_new\\" + newFonts[f0].data.Element("font").Element("image").Value;
                    //
                    //    uint file_size = (uint)new FileInfo(imagePath).Length;
                    //    bread = new BinaryReader(File.Open(imagePath, FileMode.Open));
                    //    for (uint j = 0; j < file_size; j++)
                    //        bwrite.Write(bread.ReadByte());
                    //    chunk_size += file_size;
                    //}
                }
                else if (chunk_name == "AUDO" && noAUDO == false && Directory.Exists(output_folder + chunk_name))
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
                
                chunk_end = (uint)bwrite.BaseStream.Position;
                bwrite.BaseStream.Position = chunk_offset;
                bwrite.Write(chunk_size);
                bwrite.BaseStream.Position = chunk_end;
                form_size += chunk_size;
                
                System.Console.WriteLine("Chunk " + chunk_name + " offset:" + (chunk_offset-4) + " size:" + (chunk_size+8));
            }

            //MODIFIED INFO BEGIN
            form_size -= chunk_size;
            //Modified lines
            if (File.Exists(output_folder + "translate." + ext))
            {
                List<string> patch = createStrgList(output_folder + "translate." + ext);
                //string[] patch = File.ReadAllLines(output_folder + "translate.strg", System.Text.Encoding.UTF8);
                lines = (uint)patch.Count;
                if (lines != strg.Count) System.Console.WriteLine("Warning: original." + ext + " has " + strg.Count + " lines, translate." + ext + " has " + lines + " lines");
                if (lines > 0 && patch[(int)(lines - 1)].Length == 0) lines--;

                if (translatale)
                {
                    for (int f = 0; f < lines; f++)
                    {
                        string oneLine = patch[f];
                        if (strg[f] == oneLine) continue;
                        //Redirect string
                        uint lineN = (uint)(f + 1);
                        uint line_offset = STRG_offset + (lineN + 1) * 4;

                        uint line_off = (uint)bwrite.BaseStream.Position;
                        bwrite.BaseStream.Position = line_offset;
                        bwrite.Write(line_off);
                        bwrite.BaseStream.Position = line_off;
                        //Write modified line
                        chunk_size += writeString(oneLine);
                    }
                }
                else
                {
                    bool patchNumber = true;
                    for (int f = 0; f < lines; f++)
                    {
                        string oneLine = patch[f];
                        if (oneLine.IndexOf("//") == 0) continue;

                        if (patchNumber)
                        {
                            uint lineN = System.Convert.ToUInt32(oneLine);
                            uint line_offset = STRG_offset + (lineN + 1) * 4;

                            uint line_off = (uint)bwrite.BaseStream.Position;
                            bwrite.BaseStream.Position = line_offset;
                            bwrite.Write(line_off);
                            bwrite.BaseStream.Position = line_off;
                        }
                        else
                        {
                            chunk_size += writeString(oneLine);
                        }
                        patchNumber = !patchNumber;
                    }
                }
            }
            else
            {
                System.Console.WriteLine("translate.txt not found. Strings will not be modified.");
            }
            //Font strings
            for (int f0 = 0; f0 < newFonts.Count; f0++)
            {
                fontInfo fi = newFonts[f0];

                string font_name = fi.filename;
                fi.filename_offset = (uint)(bwrite.BaseStream.Position + 4);
                chunk_size += writeString(font_name);
                
                font_name = fi.data.Element("font").Element("name").Value;
                fi.fontname_offset = (uint)(bwrite.BaseStream.Position + 4);
                chunk_size += writeString(font_name);

                newFonts[f0] = fi;
            }
            //Redirect modified fonts
            for (ushort f0 = 0; f0 < newFonts.Count; f0++)
            {
                fontInfo fi = newFonts[f0];

                uint bacp = (uint)bwrite.BaseStream.Position;
                bwrite.BaseStream.Position = FONT_offset + 4 * (fi.id + 1);
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
                using (FileStream stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                {
                    Image texture = Image.FromStream(stream);
                    w = (ushort)texture.Width;
                    h = (ushort)texture.Height;
                }
                //ushort x = 0;
                //ushort y = 0;
                //ushort s = (ushort)(TXTR_count + f0 + 1);//???
                ushort x = (ushort)fi.txtr_x;
                ushort y = (ushort)fi.txtr_y;
                ushort s = (ushort)(fi.txtr_id + 1);//???

                editSprite((uint)bwrite.BaseStream.Position, x, y, w, h, s);
                bwrite.BaseStream.Position += 22;
                chunk_size += 22;
            }

            chunk_end = (uint)bwrite.BaseStream.Position;
            bwrite.BaseStream.Position = chunk_offset;
            bwrite.Write(chunk_size);
            bwrite.BaseStream.Position = chunk_end;
            form_size += chunk_size;
            //MODIFIED INFO END

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

        static uint writeString(string wrStr)
        {
            uint recordedBytes = 0;
            uint lineLen = (uint)wrStr.Length;
            uint byteLen = (uint)System.Text.Encoding.UTF8.GetByteCount(wrStr);
            bwrite.Write(byteLen);
            recordedBytes += 4;
            //fi.fontname_offset = (uint)bwrite.BaseStream.Position;
            for (int j = 0; j < lineLen; j++)
                bwrite.Write(wrStr[j]);
            recordedBytes += byteLen;
            bwrite.Write((byte)0);
            recordedBytes += 1;
            return recordedBytes;
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
            font.image_offset = (uint)bwrite.BaseStream.Position;//!
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

        static List<string> createStrgList(string fileName)
        {
            if (strgWithBr)
            {
                List<string> strg = new List<string>();

                bread = new BinaryReader(File.Open(fileName, FileMode.Open));
                long filelength = bread.BaseStream.Length;
                while (bread.BaseStream.Position < filelength - 1)
                {
                    uint string_size = bread.ReadUInt32();
                    byte[] bytes = new byte[string_size];
                    for (uint j = 0; j < string_size; j++)
                        bytes[j] = bread.ReadByte();
                    strg.Add(System.Text.Encoding.UTF8.GetString(bytes));
                }
                bread.Close();

                return strg;
            }
            else
            {
                return new List<string>(File.ReadAllLines(fileName, System.Text.Encoding.UTF8));
            }
        }
    }
}
