﻿using System;
using System.Data;
using System.IO;
using System.Linq;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace Sign_Editor
{
	public class Utils 
	{
		private static IDbConnection db;
		public static bool UseInfiniteSigns
		{
			get
			{
				var infiniteSigns = ServerApi.Plugins.FirstOrDefault(
					p => p.Plugin.Name == "InfiniteSigns");
				return infiniteSigns == null ? false : true;
			}
		}

		public static bool DbConnect()
		{
			try
			{
				switch (TShock.DB.GetSqlType())
				{
					case SqlType.Mysql:
						string[] host = TShock.Config.MySqlHost.Split(':');
						db = new MySqlConnection()
						{
							ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
								host[0],
								host.Length == 1 ? "3306" : host[1],
								TShock.Config.MySqlDbName,
								TShock.Config.MySqlUsername,
								TShock.Config.MySqlPassword)
						};
						break;
					case SqlType.Sqlite:
						var path = Path.Combine(TShock.SavePath, "signs.sqlite");
						db = new SqliteConnection(String.Format("uri=file://{0},Version=3", path));
						break;
				}
			}
			catch (Exception ex)
			{
				Log.ConsoleError("An exception has occured while attempting to connect to the sign database: {0}",
					ex.Message);
				return false;
			}
			return true;
		}

		public static Sign DbGetSign(int x, int y)
		{
			Sign sign = null;
			string query = "SELECT Text FROM Signs WHERE X=@0 AND Y=@1 AND WorldID=@2;";
			using (var reader = db.QueryReader(query, x, y, Main.worldID))
			{
				while (reader.Read())
				{
					sign = new Sign()
					{
						x = x,
						y = y,
						text = reader.Get<string>("Text")
					};
				}
			}
			return sign;
		}

		public static bool DbSetSignText(int x, int y, string text)
		{
			string query = "UPDATE Signs SET Text=@0 WHERE X=@1 AND Y=@2 AND WorldID=@3;";
			if (db.Query(query, text, x, y, Main.worldID) != 1)
			{
				return false;
			}
			return true;
		}
	}
}