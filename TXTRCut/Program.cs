using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TXTRCut
{
    class Program
    {
        struct spriteInfo
        {            
            public string spriteName;
            public int x;
            public int y;
            public int w;
            public int h;
            public int i;            
        }

        static void Main(string[] args)
        {
            string output_folder = args[0];
            if (output_folder[output_folder.Length - 1] != '\\') output_folder += '\\';
            Directory.CreateDirectory(output_folder + "Sprites");

            if (File.Exists(output_folder + "SPRT.txt"))
            {
                Bitmap cropped = null;
                //Prepare textures
                string[] texturespath = Directory.GetFiles(output_folder + "TXTR\\");
                Bitmap[] textures = new Bitmap[texturespath.Length];
                for (int i = 0; i < textures.Length; i++)
                    textures[i] = new Bitmap(Image.FromFile(output_folder + "TXTR\\" + i + ".png"));

                string[] patchLines = File.ReadAllLines(output_folder + "SPRT.txt", System.Text.Encoding.UTF8);
                for (int l = 0; l < patchLines.Length; l++)
                {
                    Console.WriteLine("Sprite " + l + " of " + patchLines.Length);
                    Console.SetCursorPosition(0, Console.CursorTop - 1);

                    if (patchLines[l].IndexOf("//") == 0) continue;
                    string[] par = patchLines[l].Split(';');
                    spriteInfo newFont = new spriteInfo();
                    newFont.spriteName = par[0];
                    newFont.x = Convert.ToInt32(par[1]);
                    newFont.y = Convert.ToInt32(par[2]);
                    newFont.w = Convert.ToInt32(par[3]);
                    newFont.h = Convert.ToInt32(par[4]);
                    newFont.i = Convert.ToInt32(par[5]);
                    //try
                    //{
                        cropped = textures[newFont.i].Clone(new Rectangle((int)newFont.x, (int)newFont.y, (int)newFont.w, (int)newFont.h), textures[newFont.i].PixelFormat);
                        cropped.Save(output_folder + "Sprites\\" + newFont.spriteName + ".png");
                        cropped.Dispose();
                    //} finally {

                    //    if(cropped != null) cropped.Dispose();
                    //}
                    //textures[newFont.i - 1].Dispose();

                }
            }
        }
    }
}
