Hello! My name is Freddy Jason (haha, just kidding), and I want to tell you how to add a new font to Undertale.

We will need:
- The game itself. This method is suitable for any game made in Game Maker.
- Game Maker: Studio. You can get it for free via, Steam.
- Graphics editor with support for alpha channel (transparency).
- Straight arms.

**1\.** Unpack the data.win file from Undertale using my utility WinExtract. To start it conveniently create a bat file. With this structure:

`winextract [path to the data.win file] [path of where to extract the files]`

`pause`

For example:

`winextract D:\Games\Undertale\data.win D:\Undertale_data\`

`pause`

Let’s take a look at what the utility has unpacked.

![](http://i74.fastpic.ru/big/2016/0126/ad/076f73117b0cb79aa3f19c4ace28b4ad.png)

- CHUNK Folder – contains whole chunks which are only necessary when packaging the game back.
- AUDO Folder – contains every sound of the game.
- FONT Folder – It contains the extracted fonts and sprites for fonts.
- FONT_new folder – It will be used to replace the fonts with new ones.
- TXTR folder – It contains sprite sheets of the game.
- original.strg file – It contains all the strings constants used in the games.
- translate.strg file – Copy of STRG.txt. It will contain the translated strings.

UPD: originally strings placed in txt file so it could be edited by Notepad. But some games uses strings with line breakers, so in txt one string split into several ones. So you have to use GameMakerStringsEditor to edit strings.

**2\.** Create a project in Game Maker: Studio. I usually load the default demo project.

![](http://i75.fastpic.ru/big/2016/0126/42/4bc0beb7ad4dba9635259fa4c5ba9142.png)

**3\.** Create a new font. You must have the same font that you use in the game. Create it and choose the Font Family you want in the new font properties. Turn anti-aliasing and high quality off for it too look exactly like Undertale fonts.

![](http://i74.fastpic.ru/big/2016/0126/1c/91b432f2e3f1db38862cd2f08d9b441c.png)

**4\.** Click in the “+” symbol to change the font range and add the necessary symbols.

![](http://i74.fastpic.ru/big/2016/0126/bd/3207bea57be86c8d2d1e38e89a7482bd.png)

**5\.** Save the project.

**6\.** Go to the demo project directory, Ex: %project folder%/fonts. Here, including the standard fonts of the game, will lie the fonts that we will add into Undertale.

**7\.** Copy new fonts and sprites into FONT_new folder.

**8\.** Now edit patch.txt.
Each line contains 5 parameters, separated by semicolons. For example:

`2;2_new.gmx;1;0;0`

The order of the parameters: the index of the replaced font, name of new font *.gmx file, the index of TXTR sheet, x and y positions on sheet.

You can replace original fonts or place modified font on any free space of TXTR sheets.

**9\.** Edit translatale.txt using ~~Notepad or TranslaTale. Remember to save the file in UTF-8!~~ StringsEditor.

**10\.** Everything is ready to be repacked with WinPack:

`winpack D:\Undertale_data\ D:\Games\Undertale\data.win`

`pause`

**11\.** Done!

![](http://i67.fastpic.ru/big/2016/0126/cc/6d8bd12b95960ae7c07aafd5b4090ecc.png)

It's very chaotic instruction. If you don't understand, please contact Skype fjay69. Please note that my time zone is - GMT + 3.

(So, yeah, this was translated by MEX, a Brazilian, translating from Russian to English :v I personally think it is indeed, very well explained.)
