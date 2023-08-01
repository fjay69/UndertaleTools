﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Xml.Linq;
using System.Text;

namespace WinExtract
{
    class Program
    {
        static BinaryReader bread;
        static BinaryWriter bwrite;
        static string input_folder;
        static uint chunk_limit;
        static uint FONT_offset;
        static uint FONT_limit;
        static uint STRG_offset;
        static string[] fontNames;
        static bool translatale;
        static bool showstringsextract;
        static bool strgWithBr = false;
        static bool correctTXTR = false;
        static bool unpackAUDO = false;
        static bool UTswitch = false;
        static bool oldUndertale = false;

        struct endFiles
        {
            public string name;
            public uint offset;
            public uint size;
        }

        struct spriteInfo
        {
            public uint x;
            public uint y;
            public uint w;
            public uint h;
            public uint i;
        }

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: winextract <output .win file> <input folder>");
                return;
            }
            string output_win = args[0];
            input_folder = args[1];
            if (input_folder[input_folder.Length - 1] != '\\') input_folder += '\\';
            for (int i=0; i<args.Length; i++)
            {
                if (args[i] == "-tt") translatale = true;
                if (args[i] == "-showstringsextract") showstringsextract = true;
                if (args[i] == "-correctTXTR") correctTXTR = true;
                if (args[i] == "-unpackAUDO") unpackAUDO = true;
                if (args[i] == "-switch") UTswitch = true;
                if (args[i] == "-strgWithBr") strgWithBr = true;
            }
            translatale = true;
            
            using (var stream = File.OpenRead(output_win))
            {
                var md5 = System.Security.Cryptography.MD5.Create();
                byte[] hashByte = md5.ComputeHash(stream);
                StringBuilder sBuilder = new StringBuilder();
                for (int i = 0; i < hashByte.Length; i++)
                {
                    sBuilder.Append(hashByte[i].ToString("x2"));
                }
                string hashString = sBuilder.ToString();
                if (hashString == "ee918f25277cefdc6a56860d9217c34d")//Undertale 1.08c RUS
                {
                    oldUndertale = true;
                }
            }

            uint full_size = (uint)new FileInfo(output_win).Length;
            bread = new BinaryReader(File.Open(output_win, FileMode.Open));
            Directory.CreateDirectory(input_folder + "CHUNK");
            Directory.CreateDirectory(input_folder + "FONT");

            uint chunk_offset = 0;

            while (chunk_offset < full_size)
            {
                string chunk_name = new String(bread.ReadChars(4));
                uint chunk_size = bread.ReadUInt32();
                chunk_offset = (uint)bread.BaseStream.Position;
                chunk_limit = chunk_offset + chunk_size;
                System.Console.WriteLine("Chunk "+chunk_name+" offset:"+chunk_offset+" size:"+chunk_size);

                List<endFiles> filesToCreate = new List<endFiles>();

                if (chunk_name == "FORM")
                {
                    full_size = chunk_limit;
                    chunk_size = 0;
                }
                else if (chunk_name == "SPRT")
                {
                    StreamWriter tpag = new StreamWriter(input_folder + "SPRT.txt", false, System.Text.Encoding.ASCII);
                    uint sprite_count = bread.ReadUInt32();
                    long sprtoffset = bread.BaseStream.Position;
                    uint i = 0;
                    for (; i < sprite_count; sprtoffset += 4, i++)
                    {
                        bread.BaseStream.Position = sprtoffset;
                        long sprt = bread.ReadUInt32();
                        bread.BaseStream.Position = sprt;
                        string what = getSTRGEntry(bread.ReadUInt32());
                        bread.BaseStream.Position += (UTswitch ? 0x48 : 0x34);//Skip
                        //TPAG
                        uint spritec = bread.ReadUInt32();
                        for (int k = 0; k < spritec; k++)
                        {
                            tpag.Write(what + "(" + k + ")");//Sprite name
                            tpag.Write(";");

                            long backup = bread.BaseStream.Position;
                            sprt = bread.ReadUInt32();
                            bread.BaseStream.Position = sprt;
                            tpag.Write(bread.ReadInt16());//x
                            tpag.Write(";");
                            tpag.Write(bread.ReadInt16());//y
                            tpag.Write(";");
                            tpag.Write(bread.ReadInt16());//w1
                            tpag.Write(";");
                            tpag.Write(bread.ReadInt16());//h1
                            tpag.Write(";");
                            bread.BaseStream.Position += 12;
                            tpag.Write(bread.ReadInt16());//txtr id
                            tpag.Write((char)0x0D);
                            tpag.Write((char)0x0A);
                            bread.BaseStream.Position = backup;
                            bread.BaseStream.Position += 4;
                        }
                    }
                    tpag.Close();
                    bread.BaseStream.Position = chunk_limit;
                }
                else if (chunk_name == "STRG")
                {
                    STRG_offset = (uint)bread.BaseStream.Position;

                    recordStrgList();//Beta! into txt (for Undertale)

                    long bacp = bread.BaseStream.Position;
                    recordFiles(collectFonts(input_folder), "FONT");
                    bread.BaseStream.Position = bacp;
                    filesToCreate.Clear();
                }
                else if (chunk_name == "TXTR")
                {
                    List<uint> entries = collect_entries(false, correctTXTR);
                    if (!correctTXTR)
                    {
                        for (int i = 0; i < entries.Count - 1; i++)
                        {
                            uint offset = entries[i];
                            bread.BaseStream.Position = offset + (UTswitch ? 8 : 4);
                            offset = bread.ReadUInt32();
                            entries[i] = offset;
                        }
                    }
                    filesToCreate = new List<endFiles>();
                    for (int i = 0; i < entries.Count - 1; i++)
                    {
                        uint offset = entries[i];
                        uint next_offset = entries[i + 1];
                        uint size = next_offset - offset;
                        endFiles f1 = new endFiles();
                        f1.name = "" + i + ".png";
                        f1.offset = offset;
                        f1.size = size;
                        filesToCreate.Add(f1);
                    }
                }
                else if (chunk_name == "AUDO" && unpackAUDO == true)
                {
                    List<uint> entries = collect_entries(false);
                    filesToCreate = new List<endFiles>();
                    for (int i = 0; i < entries.Count - 1; i++)
                    {
                        uint offset = entries[i];
                        bread.BaseStream.Position = offset;
                        uint size = bread.ReadUInt32();
                        offset = (uint)bread.BaseStream.Position;
                        endFiles f1 = new endFiles();
                        f1.name = "" + i + ".wav";
                        f1.offset = offset;
                        f1.size = size;
                        filesToCreate.Add(f1);
                    }
                }
                else if(chunk_name == "FONT") {
                    FONT_offset = (uint)bread.BaseStream.Position;
                    FONT_limit = chunk_limit;
                }

                if (chunk_name != "FORM")
                    if (filesToCreate.Count == 0)
                    {
                        string name = "CHUNK//" + chunk_name + ".chunk";
                        uint bu = (uint)bread.BaseStream.Position;
                        bread.BaseStream.Position = chunk_offset;

                        bwrite = new BinaryWriter(File.Open(input_folder + name, FileMode.Create));                        
                        bwrite.Write(bread.ReadBytes((int)chunk_size));
                        bwrite.Close();
                        bread.BaseStream.Position = bu;

                        if (chunk_name == "CODE")
                        {
                            using (var stream = File.OpenRead(input_folder + name))
                            {
                                var md5 = System.Security.Cryptography.MD5.Create();
                                byte[] hashByte = md5.ComputeHash(stream);
                                StringBuilder sBuilder = new StringBuilder();
                                for (int i = 0; i < hashByte.Length; i++)
                                {
                                    sBuilder.Append(hashByte[i].ToString("x2"));
                                }
                                string hashString = sBuilder.ToString();
                                if (hashString == "ff44e9b4b88209202af1b73d7b187d5f")//Undertale 1.01
                                {
                                    //oldUndertale = true;
                                    unpackAUDO = true;
                                }
                            }                            
                        }
                    }
                else 
                {
                    recordFiles(filesToCreate, chunk_name);
                    
                    if (chunk_name == "TXTR") collectFontImages();
                }

                chunk_offset += chunk_size;
                bread.BaseStream.Position = chunk_offset;
            }

            if (translatale)
            {
                string ext = strgWithBr ? "strg" : "txt";
                if (!File.Exists(input_folder + "translate." + ext)) { 
                    //File.Copy(input_folder + "original."+ext, input_folder + "translate."+ext);
                }
            }
            else
                File.Open(input_folder + "translate.txt", FileMode.OpenOrCreate);

            Directory.CreateDirectory(input_folder + "FONT_new");
            File.Open(input_folder + "FONT_new\\patch.txt", FileMode.OpenOrCreate);

            //Console.Write("Done! Press any key to exit.");
            //Console.ReadKey();
        }

        static List<uint> collect_entries(bool fnt, bool correctTXTR_=false)
        {
            List<uint> entries = new List<uint>();
            uint files = bread.ReadUInt32();
            for(uint i = 0; i < files; i++)
            {
                uint offset = bread.ReadUInt32();
                if (offset != 0)
                {
                    entries.Add(offset);
                }
            }
            if (correctTXTR_)
            {
                for (int i = 0; i < files; i++)
                {
                    long bacup = bread.BaseStream.Position;
                    bread.BaseStream.Position = entries[i];
                    bread.BaseStream.Position += 8;//00 00 00 00 FF FF FF FF
                    entries[i] = bread.ReadUInt32();
                    bread.BaseStream.Position = bacup;
                }
            }
            entries.Add(fnt ? FONT_limit : chunk_limit);
            return entries;
        }

        static void recordFiles(List<endFiles> files, string folder) {
            Directory.CreateDirectory(input_folder + folder);
            for (int i = 0; i < files.Count; i++)
            {
                string name = files[i].name;
                uint bu = (uint)bread.BaseStream.Position;
                bread.BaseStream.Position = files[i].offset;

                bwrite = new BinaryWriter(File.Open(input_folder + folder + "\\" + name, FileMode.Create));
                bwrite.Write(bread.ReadBytes((int)files[i].size));
                bwrite.Close();
                bread.BaseStream.Position = bu;
            }
        }

        static List<endFiles> collectFonts(string input_folder) {
            bread.BaseStream.Position = FONT_offset;
            List<uint> entries = collect_entries(true);
            List<endFiles> filesToCreate = new List<endFiles>();

            StreamWriter fontsIndex = new StreamWriter(input_folder + "FONT\\" + "fonts.txt", false, System.Text.Encoding.UTF8);
            fontNames = new string[entries.Count];

            for (int i = 0; i < entries.Count-1; i++)
            {
                XDocument xmldoc = new XDocument();
                XElement xfont = new XElement("font");
                
                uint offset = entries[i];
                bread.BaseStream.Position = offset;
                string font_name = getSTRGEntry(bread.ReadUInt32());
                
                fontNames[i] = font_name;
                               
                xfont.Add(new XElement("name", getSTRGEntry(bread.ReadUInt32())));
                xfont.Add(new XElement("size", bread.ReadUInt32()));
                xfont.Add(new XElement("bold", bread.ReadUInt32()));
                xfont.Add(new XElement("italic", bread.ReadUInt32()));

                XElement xrange = new XElement("ranges");
                ushort lrange = bread.ReadUInt16();
                bread.ReadUInt16();
                ushort urange = bread.ReadUInt16();
                bread.ReadUInt16();
                xrange.Add(new XElement("range0",""+lrange+","+urange));
                xfont.Add(xrange);

                spriteInfo fontSprite = getSpriteInfo(bread.ReadUInt32());
                fontsIndex.WriteLine(i.ToString() + ";" + font_name + ".font.gmx;"+ fontSprite.i+";"+fontSprite.x+";"+fontSprite.y);
                bread.BaseStream.Position += 8;
                uint glyphCount = bread.ReadUInt32();
                xrange = new XElement("glyphs");
                for (uint g=0; g<glyphCount; g++)
                {
                    uint glyphOffset = bread.ReadUInt32();
                    long bacp = bread.BaseStream.Position;
                    bread.BaseStream.Position = glyphOffset;

                    XElement xglyph = new XElement("glyph");
                    xglyph.SetAttributeValue("character", bread.ReadUInt16());
                    xglyph.SetAttributeValue("x", bread.ReadUInt16());
                    xglyph.SetAttributeValue("y", bread.ReadUInt16());
                    xglyph.SetAttributeValue("w", bread.ReadUInt16());
                    xglyph.SetAttributeValue("h", bread.ReadUInt16());
                    xglyph.SetAttributeValue("shift", bread.ReadUInt16());
                    xglyph.SetAttributeValue("offset", bread.ReadInt16());//sic!
                    xrange.Add(xglyph);

                    bread.BaseStream.Position = bacp;
                }
                xfont.Add(xrange);

                xfont.Add(new XElement("image", ""+font_name+".png"));

                xmldoc.Add(xfont);
                StreamWriter tpag = new StreamWriter(input_folder + "FONT\\" + font_name + ".font.gmx", false, Encoding.UTF8);
                tpag.Write(xmldoc.ToString());
                tpag.Close();
            }
            fontsIndex.Close();
            return filesToCreate;
        }

        static uint calculateFontSize(uint font_offset) {
            uint result = 44;
            long bacup = bread.BaseStream.Position;

            bread.BaseStream.Position = font_offset+40;
            uint glyphs = bread.ReadUInt32();
            result += glyphs * 20;

            bread.BaseStream.Position = bacup;
            return result;
        }

        static void collectFontImages()
        {
            long bacup = bread.BaseStream.Position;
            bread.BaseStream.Position = FONT_offset;
            List<uint> fonts = collect_entries(false);
            //Console.WriteLine("i\tx\ty\tw\th");
            for (int f = 0; f < fonts.Count - 1; f++)
            {
                bread.BaseStream.Position = fonts[f] + 28;
                spriteInfo sprt = getSpriteInfo(bread.ReadUInt32());
                //Console.WriteLine(""+sprt.i+"\t"+ sprt.x + "\t" + sprt.y + "\t" + sprt.w + "\t" + sprt.h);
                using (var texture = new Bitmap(Image.FromFile(input_folder + "TXTR\\" + sprt.i + ".png")))
                {                    
                    using (Bitmap cropped = texture.Clone(new Rectangle((int)sprt.x, (int)sprt.y, (int)sprt.w, (int)sprt.h), texture.PixelFormat)) 
                        cropped.Save(input_folder + "FONT\\" + fontNames[f] + ".png");                    
                }
            }
            bread.BaseStream.Position = bacup;
        }

        static spriteInfo getSpriteInfo(uint sprite_offset)
        {
            spriteInfo result = new spriteInfo();
            long bacup = bread.BaseStream.Position;
            bread.BaseStream.Position = sprite_offset;
            result.x = bread.ReadUInt16();
            result.y = bread.ReadUInt16();
            result.w = bread.ReadUInt16();
            result.h = bread.ReadUInt16();
            bread.BaseStream.Position += 12;
            result.i = bread.ReadUInt16();
            if(oldUndertale)
                if(result.i>20) result.i--;//Some old versions
            bread.BaseStream.Position = bacup;
            return result;
        }

        static string getSTRGEntry(uint str_offset)
        {
            long bacup = bread.BaseStream.Position;
            bread.BaseStream.Position = str_offset-4;//???
            
            uint string_size = bread.ReadUInt32();
            byte[] bytes = bread.ReadBytes((int)string_size);             
            bread.BaseStream.Position = bacup;
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        static byte[] getSTRGEntryByte(uint str_offset)
        {
            byte sy;
            long bacup = bread.BaseStream.Position;
            bread.BaseStream.Position = str_offset;

            uint string_size = bread.ReadUInt32();
            List<byte> listByte = new List<byte>();

            sy = bread.ReadByte();
            while (sy != 0)
            {
                listByte.Add(sy);
                sy = bread.ReadByte();
            }
            bread.BaseStream.Position = bacup;

            byte[] bytes = listByte.ToArray();
            return bytes;
        }

        static void recordStrgList()
        {  
            uint strings = bread.ReadUInt32();
            recordStrgTranslated(strings);
            //bread.BaseStream.Position += strings * 4;//Skip offsets
            byte sy;
            bwrite = new BinaryWriter(File.Open(input_folder + "original." + (strgWithBr ? "strg" : "txt"), FileMode.Create));
            if (strgWithBr) {
                for (uint i = 0; i < strings; i++)
                {
                    uint string_size = bread.ReadUInt32();//00 after string
                    bwrite.Write(string_size);                    
                    bwrite.Write(bread.ReadBytes((int)string_size));
                    bread.BaseStream.Position++;
                }
            } else {
                for (uint i = 0; i < strings; i++)
                {
                    uint string_size = bread.ReadUInt32();//Bytes or Unicode symbols?
                    if (i < strings - 1) string_size++;
                    sy = bread.ReadByte();
                    for (; sy!=0x00; sy = bread.ReadByte()) {
                    //for (uint j = 0; j < string_size; j++) {
                        if (sy == 0x0D || sy == 0x0A)
                        {
                            System.Console.WriteLine("Warning: string " + i + " have line brake. Will be replaced with space.");
                            bwrite.Write((byte)0x20);
                        }
                        else
                            bwrite.Write(sy);
                    }
                    //bwrite.BaseStream.Position--;
                    if (i < strings - 1) {
                        bwrite.Write((byte)0x0D);
                        bwrite.Write((byte)0x0A);
                    }
                }
            }
            bwrite.Close();
        }

        static void recordStrgTranslated(uint strings)
        {
            byte sy;
            bwrite = new BinaryWriter(File.Open(input_folder + "translate.txt", FileMode.Create));
            for (uint i = 0; i < strings; i++)
            {
                if (showstringsextract)
                {
                    Console.WriteLine("String " + i + " of " + strings);
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                }
                byte[] strByte = getSTRGEntryByte(bread.ReadUInt32());
                for (uint j = 0; j < strByte.Length; j++)
                {
                    sy = strByte[j];
                    if (sy == 0x0D || sy == 0x0A)
                    {
                        //System.Console.WriteLine("Warning: string " + i + " have line brake. Will be replaced with space.");
                        bwrite.Write((byte)0x20);
                    }
                    else
                        bwrite.Write(sy);
                }
                if (i < strings - 1)
                {
                    bwrite.Write((byte)0x0D);
                    bwrite.Write((byte)0x0A);
                }
            }
            bwrite.Close();
        }
    }
}
