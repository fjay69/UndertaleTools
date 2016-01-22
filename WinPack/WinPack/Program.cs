using System;
using System.Collections.Generic;
using System.IO;

namespace WinPack
{
    class Program
    {
        static BinaryWriter bwrite;
        static BinaryReader bread;
        static uint form_size;
        static uint FONT_offset;
        static string[] chunks = new string[] { "GEN8","OPTN","EXTN","SOND","AGRP","SPRT","BGND","PATH","SCPT","SHDR","FONT","TMLN","OBJT","ROOM","DAFL","TPAG","CODE","VARI",
                                    "FUNC","STRG","TXTR","AUDO" };

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                System.Console.WriteLine("Usage: winpack <output folder> <input .win file>");
                return;
            }
            string output_folder = args[0];
            if (output_folder[output_folder.Length-1]!='\\') output_folder += '\\';
            string input_win = args[1];
            form_size = 0;
            
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
                        if (patch[(int)(lines - 1)].Length == 0) lines--;
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
                    else {
                        System.Console.WriteLine("translate.txt not found. Strings will not be modified.");
                    }
                    
                    //Вшиваем шрифты                    
                    string patch_path = output_folder+"patch\\";
                    if (File.Exists(patch_path + "patch.txt"))
                    {
                        StreamReader patchText = new StreamReader(patch_path + "patch.txt", System.Text.Encoding.ASCII);
                        for (string oneLine = patchText.ReadLine(); oneLine != null; oneLine = patchText.ReadLine())
                        {
                            if (oneLine.IndexOf("//") == 0) continue;
                            string[] par = oneLine.Split(';');

                            int index_replaced = Convert.ToInt32(par[0]);
                            string old_font_name = par[1];
                            string new_font_name = par[2];
                            ushort x = Convert.ToUInt16(par[3]);
                            ushort y = Convert.ToUInt16(par[4]);
                            ushort w = Convert.ToUInt16(par[5]);
                            ushort h = Convert.ToUInt16(par[6]);
                            ushort s = Convert.ToUInt16(par[7]);

                            uint bacp = (uint)bwrite.BaseStream.Position;
                            bwrite.BaseStream.Position = FONT_offset + 4 * (index_replaced + 1);//!!!
                            bwrite.Write(bacp);
                            bwrite.BaseStream.Position = bacp;

                            //Подготовка шрифта
                            uint font_size = (uint)new FileInfo(patch_path + "\\" + new_font_name).Length;
                            BinaryReader new_font_file = new BinaryReader(File.Open(patch_path + "\\" + new_font_name, FileMode.Open));
                            BinaryReader old_font_file = new BinaryReader(File.Open(patch_path + "\\" + old_font_name, FileMode.Open));

                            for (uint j = 0; j < 8; j++)
                                bwrite.Write(old_font_file.ReadByte());//Name and font family
                            new_font_file.BaseStream.Position += 8;
                            for (uint j = 0; j < 20; j++)
                                bwrite.Write(new_font_file.ReadByte());
                            old_font_file.BaseStream.Position += 20;
                            uint sprite_offset = old_font_file.ReadUInt32();

                            editSprite(sprite_offset, x, y, w, h, s);//!!!

                            bwrite.Write(sprite_offset);
                            new_font_file.BaseStream.Position += 4;
                            for (uint j = 0; j < 8; j++)
                                bwrite.Write(new_font_file.ReadByte());
                            uint glyph_count = new_font_file.ReadUInt32();
                            bwrite.Write(glyph_count);

                            for (uint j = 0; j < glyph_count; j++)
                                bwrite.Write((uint)(bwrite.BaseStream.Position + (glyph_count - j) * 4 + j * 16));
                            new_font_file.BaseStream.Position += glyph_count * 4;
                            for (uint j = 0; j < (glyph_count) * 16; j++)
                                bwrite.Write(new_font_file.ReadByte());

                            chunk_size += font_size;
                        }
                    }
                    else {
                        System.Console.WriteLine("patch.txt not found. Fonts will not be modified.");
                    }

                    bwrite.Write((uint)0); chunk_size += 4;
                }
                else if (chunk_name == "TXTR")
                {
                    uint files = (uint)Directory.GetFiles(output_folder + chunk_name).Length;
                    bwrite.Write(files);
                    chunk_size += 4;

                    uint[] Offsets = new uint[files];
                    
                    //Headers offset
                    for (int f=0; f<files; f++)
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

                        bwrite.Write((uint)1);
                        Offsets[f] = (uint)bwrite.BaseStream.Position;
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
                    for (uint f0 = 0; f0 < files; f0++)
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
        }

        static void editSprite(uint sprite_offset, ushort x, ushort y, ushort w, ushort h, ushort s) {
            uint bacp = (uint)bwrite.BaseStream.Position;
            bwrite.BaseStream.Position = sprite_offset;
            bwrite.Write(x);
            bwrite.Write(y);
            bwrite.Write(w);
            bwrite.Write(h);
            bwrite.BaseStream.Position += 4;
            bwrite.Write(w);
            bwrite.Write(h);
            bwrite.Write(w);
            bwrite.Write(h);
            bwrite.Write(s);
            bwrite.BaseStream.Position = bacp;
        }       
    }
}
