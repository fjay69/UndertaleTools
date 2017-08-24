using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameMakerStringsEditor
{
    public partial class Form1 : Form
    {
        int searchIndex = 0;

        public Form1()
        {
            InitializeComponent();        
        }

        private void button1_Click(object sender, EventArgs e)
        {            
            var winFileDialog = new OpenFileDialog();
            winFileDialog.Filter = "String data (*.strg) | *.strg";

            if (winFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string strgFile = winFileDialog.FileName;
                
                multiLineListBox1.Items.Clear();

                BinaryReader bread = new BinaryReader(File.Open(strgFile, FileMode.Open));

                long filelength = bread.BaseStream.Length;

                while (bread.BaseStream.Position < filelength-1)
                {                    
                    uint string_size = bread.ReadUInt32();
                    byte[] bytes = new byte[string_size];
                    for (uint j = 0; j < string_size; j++)
                        bytes[j] = bread.ReadByte();
                    multiLineListBox1.Items.Add(System.Text.Encoding.UTF8.GetString(bytes));
                }
                bread.Close();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var winFileDialog = new SaveFileDialog();
            winFileDialog.Filter = "String data (*.strg) | *.strg";

            if (winFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BinaryWriter bwrite = new BinaryWriter(File.Open(winFileDialog.FileName, FileMode.Create));

                for (int i = 0; i < multiLineListBox1.Items.Count; i++)
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(multiLineListBox1.Items[i].ToString());
                    bwrite.Write((uint)bytes.Length);
                    for (uint j = 0; j < bytes.Length; j++)
                        bwrite.Write(bytes[j]);
                }

                bwrite.Close();
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            searchIndex = 0;
            button3_Click(sender, e);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            int finded = multiLineListBox1.FindString(textBox1.Text, searchIndex);
            if (finded != -1)
            {
                multiLineListBox1.SetSelected(finded, true);
                searchIndex = finded;
            }
        }
    }
}
