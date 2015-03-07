﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace Sign_Editor
{
	[ApiVersion(1, 17)]
    public class SignEditor : TerrariaPlugin
	{
		public SignEditor(Main game)
			: base(game)
		{
			Order = 2;
		}

		public override Version Version
		{
			get
			{
				return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
			}
		}

		public override string Name
		{
			get
			{
				return "Sign Editor";
			}
		}

		public override string Author
		{
			get
			{
				return "Enerdy";
			}
		}

		public override string Description
		{
			get
			{
				return "Load and save sign content to text files.";
			}
		}

		SMemory[] Memory = new SMemory[Main.maxPlayers];
		bool UsingInfiniteSigns;

		public override void Initialize()
		{
			UsingInfiniteSigns = ServerApi.Plugins.Any(p =>
				p.Plugin.Name.Equals("InfiniteSigns", StringComparison.InvariantCulture));

			ServerApi.Hooks.NetGetData.Register(this, OnGetData);

			#region Commands

			Commands.ChatCommands.Add(new Command(Permissions.Info, DoSignInfo, "sign")
			{
				HelpDesc = Help.Info
			});

			Commands.ChatCommands.Add(new Command(Permissions.Load, DoSignLoad, "signload", "sload")
			{
				AllowServer = false,
				HelpDesc = Help.Load
			});

			Commands.ChatCommands.Add(new Command(Permissions.Save, DoSignSave, "signsave", "ssave")
			{
				AllowServer = false,
				HelpDesc = Help.Save
			});

			Commands.ChatCommands.Add(new Command(
				new List<string>()
				{
					Permissions.Load,
					Permissions.Save,
					Permissions.Clipboard
				},
				DoSignClear, "signclear", "sclear")
				{
					AllowServer = false,
					HelpText = "Cancels the current sign action and empties the clipboard."
				});

			Commands.ChatCommands.Add(new Command(Permissions.Clipboard, DoSignCopy, "signcopy", "scopy")
			{
				AllowServer = false,
				HelpDesc = Help.Copy
			});

			Commands.ChatCommands.Add(new Command(Permissions.Clipboard, DoSignPaste, "signpaste", "spaste")
			{
				AllowServer = false,
				HelpDesc = Help.Paste
			});

			Commands.ChatCommands.Add(new Command(Permissions.Files, DoSignFiles, "signfiles", "sfiles")
			{
				AllowServer = false,
				HelpText = "Returns a list of all valid files for reading inside the Sign Editor folder."
			});

			#endregion

			if (!FileTools.CheckDir(FileTools.DirPath))
				TShock.Log.ConsoleInfo("Created Sign Editor directory.");
			if (UsingInfiniteSigns)
				Utils.DbConnect();

			// Debug command to check if InfiniteSigns is in use
			//Commands.ChatCommands.Add(new Command(AmIUsingIS, "checkis"));
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
			}
		}

		void OnGetData(GetDataEventArgs args)
		{
			var ply = args.Msg.whoAmI;
			if (args.MsgID == PacketTypes.SignRead && Memory[ply].Active)
			{
				using (var reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
				{
					int x = reader.ReadInt16();
					int y = reader.ReadInt16();
					int signID = Sign.ReadSign(x, y);

					Sign sign = UsingInfiniteSigns ? Utils.DbGetSign((int)x, (int)y) : Main.sign[signID];
					if (sign == null)
						TShock.Log.Debug("Utils.DbGetSign(int x, int y) returned null.");
					else
					{
						switch (Memory[ply].Action)
						{
							case SignAction.LOAD:
								if (UsingInfiniteSigns)
								{
									var text = FileTools.Load(Memory[ply].File);
									if (!Utils.DbSetSignText(sign.x, sign.y, text))
									{
										TShock.Players[ply].SendErrorMessage(
											"Failed to load to InfiniteSigns sign.");
										break;
									}
								}
								else
								{
									Sign.TextSign(signID, FileTools.Load(Memory[ply].File));
								}
								TShock.Players[ply].SendInfoMessage(String.Format(
									"Loaded file '{0}' to sign.", Memory[ply].File));
								break;
							case SignAction.SAVE:
								if (FileTools.Save(Memory[ply].File, sign.text))
								{
									TShock.Players[ply].SendInfoMessage(String.Format(
										"Saved sign's contents to file '{0}'.", Memory[ply].File));
								}
								else
								{
									TShock.Players[ply].SendErrorMessage(
										"Failed to save to file. Check logs for details.");
								}
								break;
							case SignAction.COPY:
								Memory[ply].Clipboard = sign.text;
								TShock.Players[ply].SendInfoMessage(
									"Copied sign's contents to clipboard.");
								break;
							case SignAction.PASTE:
							case SignAction.PERSISTENT:
								if (UsingInfiniteSigns)
								{
									var text = Memory[ply].Clipboard;
									if (!Utils.DbSetSignText(sign.x, sign.y, text))
									{
										TShock.Players[ply].SendErrorMessage(
											"Failed to paste to InfiniteSigns sign.");
										break;
									}
								}
								else
								{
									Sign.TextSign(signID, Memory[ply].Clipboard);
								}
								TShock.Players[ply].SendInfoMessage("Pasted selection.");
								break;
						}
						Memory[ply].Active = Memory[ply].Action == SignAction.PERSISTENT;
						args.Handled = true;
					}
				}
			}
		}

		void DoSignInfo(CommandArgs args)
		{
			int pageNumber;
			if (!PaginationTools.TryParsePageNumber(args.Parameters, 0, args.Player, out pageNumber))
			{
				pageNumber = 1;
			}
			PaginationTools.SendPage(args.Player, pageNumber, Help.Info,
				new PaginationTools.Settings()
				{
					IncludeHeader = false,
					FooterFormat = "Type {0}sign {{0}} for more info.".SFormat(Commands.Specifier)
				});
		}

		void DoSignLoad(CommandArgs args)
		{
			var count = args.Parameters.Count;
			if (count == 0)
			{
				args.Player.SendInfoMessage(
						"Usage: {0}signload <filename>. Type {0}help signload for more info.",
						Commands.Specifier);
			}
			else
			{
				var i = args.Player.Index;
				Memory[i].Action = SignAction.LOAD;
				Memory[i].File = string.Join(" ", args.Parameters);
				var test = Path.Combine(FileTools.DirPath, Memory[i].File);
				if (!File.Exists(test))
				{
					args.Player.SendErrorMessage("File doesn't exist!");
					return;
				}
				Memory[i].Active = true;
				args.Player.SendInfoMessage("Loading from file. Read a sign to continue.");
			}
		}

		void DoSignSave(CommandArgs args)
		{
			var count = args.Parameters.Count;
			if (count == 0)
			{
				args.Player.SendInfoMessage(
					"Usage: {0}signsave <filename>. Type {0}help signsave for more info.",
					Commands.Specifier);
			}
			else
			{
				var i = args.Player.Index;
				Memory[i].Action = SignAction.SAVE;
				Memory[i].Active = true;
				Memory[i].File = string.Join(" ", args.Parameters);
				args.Player.SendInfoMessage("Saving to file. Read a sign to continue.");
			}
		}

		void DoSignClear(CommandArgs args)
		{
			Memory[args.Player.Index].Active = false;
			Memory[args.Player.Index].Clipboard = String.Empty;
			args.Player.SendInfoMessage("Cleared sign action and clipboard.");
		}

		void DoSignCopy(CommandArgs args)
		{
			var i = args.Player.Index;
			Memory[i].Action = SignAction.COPY;
			Memory[i].Active = true;
			args.Player.SendInfoMessage("Copying to clipboard. Read a sign to continue.");
		}

		void DoSignPaste(CommandArgs args)
		{
			var i = args.Player.Index;

			// Clipboard Check
			if (String.IsNullOrEmpty(Memory[i].Clipboard))
			{
				args.Player.SendErrorMessage("Clipboard cannot be empty!");
				return;
			}
			// Cancel persistent mode
			if (Memory[i].Action == SignAction.PERSISTENT)
			{
				Memory[i].Active = false;
				args.Player.SendInfoMessage("Cancelled pasting.");
				return;
			}

			Memory[i].Active = true;
			string mode = string.Empty;
			if (args.Parameters.Count > 0 && args.Parameters[0].StartsWith("-p"))
			{
				Memory[i].Action = SignAction.PERSISTENT;
				mode = " with persistent mode";
			}
			else
			{
				Memory[i].Action = SignAction.PASTE;
			}
			args.Player.SendInfoMessage(
				"Pasting from clipboard{0}. Read a sign to continue.", mode);
			if (!String.IsNullOrEmpty(mode))
				args.Player.SendInfoMessage(
					"Type {0}signpaste again to cancel persistent mode.", Commands.Specifier);
		}

		void DoSignFiles(CommandArgs args)
		{
			int pageNumber;
			var files = FileTools.ListFiles();
			var lines = PaginationTools.BuildLinesFromTerms(files);
			if (!PaginationTools.TryParsePageNumber(args.Parameters, 0, args.Player, out pageNumber))
			{
				pageNumber = 1;
			}
			PaginationTools.SendPage(args.Player, pageNumber, lines,
				new PaginationTools.Settings()
				{
					HeaderFormat = "Available Files ({0}/{1}):",
					FooterFormat = "Type {0}signfiles {{0}} for more files.".SFormat(Commands.Specifier),
					NothingToDisplayString = "No files available."
				});
		}

		void AmIUsingIS(CommandArgs args)
		{
			args.Player.SendInfoMessage("UsingInfiniteSigns: {0}", UsingInfiniteSigns);
		}
	}
}
