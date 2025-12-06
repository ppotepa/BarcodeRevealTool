 Coding Day : Barcode Reveal Tool Bcuz Nobody Likes Them

 By creating this tool we are aiming to improve Ladder Experience at any Level
 I've had this tool created last year, but that tool was very messy, also didnt work 100% of times,
 and didnt gather data about players you play (barcodes and their last build order they played)

 lets be honest 90% of time even 95% when you play sb  they play exactly the same build order
 this is the main reason why |||||||||| is ruining the ladder experience for most ppl 
 then next game you play you have no clue its the same dude under the barcode or not

 When we play the game a temporary file is being stored on our hard drive, like some sort of cache
 and possibly containing replay data, this is what we are looking for

 We dont want to create a Maphack so we only care about reading user Battle Tag or Nickname from the replay

 In my case the file we are looking for can be found in temporary files, 
 FIle is being created each time you start a ranked game
 WHen you are back to the lobby files are being deleted

 C:\Users\pawel\AppData\Local\Temp\StarCraft II\TempWriteReplayP1\replay.server.battlelobby 
 AND YES.. THIS IS MOST LIKELY HOW MAP HACKS CAN BE CREATED also by reading some additional data from memory i believe
 OF literally reading the replay that is being created on the fly via memory stream or that type of stuff... 

1. Decoding replay.server.battlelobby  file, honestly i will use GPT for that >.< its most likely a binary file, but 
im not that keen on spending 3 hrs on decoding that... 

after a bit of search 

we can find all thje data we need in a block : 
(quick peek with any hex editor)

So originaror is me, and nina williams is a battle tag of the other person, not sure if it was a barcode in this case
we can store that data and display all build orders played by this certain battle tag
so we either gonna be seeking a regex match here, or we check couple of games if addresss is always the same 
which im gonna do Rn


[GAME 1]

00006E00                                            4D 65                Me
00006E10  63 68 47 75 79 76 65 72 23 31 34 33 0B 02 00 01  chGuyver#143....
00006E20  4C 32 00 00 00 01 0C 4D 65 63 68 47 75 79 76 65  L2.....MechGuyve
00006E30  72 23 31 34 33 01 00 17 81 58 29 0C 44 E5 CA FE  r#143....X).DåÊþ
00006E40  BA BE 68 E1 F1 15 00 00 00 00 00 00 00 00 04 0D  º¾háñ...........
00006E50  00 9C 80 01 23 00 01 00 00 80 00 00 08 00 24 00  .œ€.#....€....$.
00006E60  4F 72 69 67 69 6E 61 74 6F 72 23 32 31 33 34 33  Originator#21343
00006E70  81 00 07 57 03 00 00 00 09 15 05 00 0C C0 48 01  ...W.........ÀH.
00006E80  02 00 05 33 02 00 00 00 01 00 00 00 00 0A 97 43  ...3..........—C
00006E90  0D 02 00 05 33 02 00 00 00 11 06 4E 69 6E 61 57  ....3......NinaW
00006EA0  69 6C 6C 69 61 6D 73 23 36 30 33 0B 02 00 01 4C  illiams#603....L
00006EB0  32 00 00 00 01 0E 4E 69 6E 61 57 69 6C 6C 69 61  2.....NinaWillia
00006EC0  6D 73 23 36 30 33 01 00 17 81 59 29 0C 44 E5 CA  ms#603....Y).DåÊ
00006ED0  FE BA BE 62 4E 00 16 00 00 00 00 00 00 00 00 04  þº¾bN...........
00006EE0  08 00 9C 80 01 23 20 01 00 00 80 00 00 08 00 24  ..œ€.# ...€....$
00006EF0  02 4E 69 6E 61 57 69 6C 6C 69 61 6D 73 23 32 31  .NinaWilliams#21
00006F00  39 35 30                                         950
   

[GAME 2]

00006E00                                            43 51                CQ
00006E10  73 68 61 6B 65 23 35 34 30 0B 02 00 01 4C 32 00  shake#540....L2.
00006E20  00 00 01 09 43 51 73 68 61 6B 65 23 35 34 30 01  ....CQshake#540.
00006E30  00 48 F0 B7 58 17 7C AB CA FE BA BE B3 F5 0F 02  .Hð·X.|«Êþº¾³õ..
00006E40  00 00 00 00 00 00 00 00 04 0D 00 9C 80 01 23 00  ...........œ€.#.
00006E50  11 04 00 80 40 01 08 00 14 03 4D 61 78 61 72 6E  ...€@.....Maxarn
00006E60  23 32 31 30 32 81 00 07 A5 03 00 00 00 05 0C 05  #2102...¥.......
00006E70  00 27 0F 03 01 02 00 05 33 02 00 00 00 01 00 00  .'......3.......
00006E80  00 00 0A 8B 9E 00 02 00 05 33 02 00 00 00 11 04  ...‹ž....3......
00006E90  4D 65 63 68 47 75 79 76 65 72 23 31 34 33 0B 02  MechGuyver#143..
00006EA0  00 01 4C 32 00 00 00 01 0C 4D 65 63 68 47 75 79  ..L2.....MechGuy
00006EB0  76 65 72 23 31 34 33 01 00 48 F0 B8 58 17 7C AB  ver#143..Hð¸X.|«
00006EC0  CA FE BA BE 68 E1 F1 15 00 00 00 00 00 00 00 00  Êþº¾háñ.........
00006ED0  04 0D 00 9C 80 01 23 00 01 00 00 80 00 00 08 00  ...œ€.#....€....
00006EE0  24 00 4F 72 69 67 69 6E 61 74 6F 72 23 32 31 33  $.Originator#213 

ok by looking at both games, they look pretty similar to me :)
mb what we can use is MemorySpan or some buffer idk