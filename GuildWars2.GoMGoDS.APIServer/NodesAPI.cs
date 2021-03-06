﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.XPath;

using GuildWars2.ArenaNet.API;
using GuildWars2.ArenaNet.Model;
using GuildWars2.GoMGoDS.API;
using GuildWars2.GoMGoDS.Model;

using HtmlAgilityPack;
using log4net;

namespace GuildWars2.GoMGoDS.APIServer
{
    public class NodesAPI : CachedAPI<NodesResponse>
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(NodesAPI));
        private static string p_ValRegexFormat = @"^.*(?:;|\s)?{0}:\s*([\d.]+)px.*$";

        private static Regex p_XVal = new Regex(string.Format(p_ValRegexFormat, "left"));
        private static Regex p_YVal = new Regex(string.Format(p_ValRegexFormat, "top"));
        private static Regex p_WVal = new Regex(string.Format(p_ValRegexFormat, "width"));
        private static Regex p_HVal = new Regex(string.Format(p_ValRegexFormat, "height"));

        private static TimeSpan p_CacheTimeout = new TimeSpan(1, 0, 0);

        private static IDictionary<int, int> p_WorldIdMap = new Dictionary<int, int>()
            {
                // NA servers
                { 1001, 47 }, // Anvil Rock
                { 1002, 48 }, // Borlis Pass
                { 1003, 38 }, // Yak's Bend
                { 1004, 32 }, // Henge of Denravi
                { 1005, 41 }, // Maguuma
                { 1006, 45 }, // Sorrow's Furnace
                { 1007, 46 }, // Gate of Madness
                { 1008, 28 }, // Jade Quarry
                { 1009, 40 }, // Fort Aspenwood
                { 1010, 44 }, // Ehmry Bay
                { 1011, 29 }, // Stormbluff Isle
                { 1012, 39 }, // Darkhaven
                { 1013, 34 }, // Sanctum of Rall
                { 1014, 37 }, // Crystal Desert
                { 1015, 31 }, // Isle of Janthir
                { 1016, 33 }, // Sea of Sorrows
                { 1017, 35 }, // Tarnished Coast
                { 1018, 43 }, // Northern Shiverpeaks
                { 1019, 30 }, // Blackgate
                { 1020, 49 }, // Ferguson's Crossing
                { 1021, 42 }, // Dragonbrand
                { 1022, 51 }, // Kaineng
                { 1023, 50 }, // Devona's Rest
                { 1024, 36 }, // Eredon Terrace

                // EU servers
                { 2001, 27 }, // Fissure of Woe
                { 2002,  4 }, // Desolation
                { 2003, 15 }, // Gandara
                { 2004,  7 }, // Blacktide
                { 2005, 20 }, // Ring of Fire
                { 2006, 22 }, // Underworld
                { 2007,  6 }, // Far Shiverpeaks
                { 2008, 24 }, // Whiteside Ridge
                { 2009, 26 }, // Ruins of Surmia
                { 2010, 10 }, // Seafarer's Rest
                { 2011, 25 }, // Vabbi
                { 2012,  1 }, // Piken Square
                { 2013, 19 }, // Aurora Glade
                { 2014, 17 }, // Gunnar's Hold
                { 2101, 16 }, // Jade Sea
                { 2102, 11 }, // Fort Ranik
                { 2103, 13 }, // Augury Rock
                { 2104,  3 }, // Vizunah Square
                { 2105,  2 }, // Arborstone
                { 2201,  9 }, // Kodash
                { 2202,  8 }, // Riverside
                { 2203,  5 }, // Elona Reach
                { 2204, 18 }, // Abaddon's Mouth
                { 2205, 21 }, // Drakkar Lake
                { 2206, 14 }, // Miller's Sound
                { 2207, 23 }, // Dzagonur
                { 2301, 12 }  // Baruch Bay
            };
        private static IDictionary<int, int> p_MapIdMap = new Dictionary<int, int>()
            {
                {   30,  3 }, // frostgorge sound
                {   65,  1 }, // malchor's leap
                {   62,  2 }, // cursed shore
                {  873,  4 }  // southsun cove
            };

        private IDbConnection m_DbConn;

        public NodesAPI()
            : base(p_CacheTimeout)
        { }

        #region APIBase
        public override string RequestPath { get { return "/nodes.json"; } }

        public override void Init(IDbConnection dbConn)
        {
            m_DbConn = dbConn;
            DbCreateTables();
        }

        protected override NodesResponse GetData(IDictionary<string, string> _get)
        {
            int worldId = -1;
            if (_get.ContainsKey("world_id"))
                int.TryParse(_get["world_id"], out worldId);

            int mapId = -1;
            if (_get.ContainsKey("map_id"))
                int.TryParse(_get["map_id"], out mapId);

            DateTime timestamp = DbGetLatestTimestamp(worldId, mapId);
            IList<NodeInfo> nodes = DbGetNodes(worldId, mapId);

            NodesResponse data = new NodesResponse()
            {
                WorldId = worldId,
                MapId = mapId,
                Timestamp = (long)(timestamp - new DateTime(1970, 1, 1)).TotalMilliseconds,
                Nodes = nodes.Select<NodeInfo, NodeLocation>(ni => new NodeLocation() { X = ni.X, Y = ni.Y, Name = ni.Name, Type = ni.Type }).ToList()
            };

            return data;
        }
        #endregion

        #region CachedAPI
        protected override DateTime GetCacheTimestamp(IDictionary<string, string> _get)
        {
            int worldId = -1;
            if (_get.ContainsKey("world_id"))
                int.TryParse(_get["world_id"], out worldId);

            int mapId = -1;
            if (_get.ContainsKey("map_id"))
                int.TryParse(_get["map_id"], out mapId);

            return DbGetLatestTimestamp(worldId, mapId);
        }

        protected override void RefreshData(IDictionary<string, string> _get)
        {
            int worldId = -1;
            if (_get.ContainsKey("world_id"))
                int.TryParse(_get["world_id"], out worldId);

            int mapId = -1;
            if (_get.ContainsKey("map_id"))
                int.TryParse(_get["map_id"], out mapId);

            DateTime timestamp = DateTime.UtcNow;

            IList<NodeInfo> nodes = GetGw2Nodes(worldId, mapId, timestamp);

            if (nodes != null)
            {
                IDbTransaction tx = m_DbConn.BeginTransaction();

                try
                {
                    DbClearNodes(worldId, mapId);

                    foreach (NodeInfo node in nodes)
                        DbSaveNode(node);

                    tx.Commit();
                }
                catch (Exception e)
                {
                    LOGGER.Error("Exception thrown when attempting to update nodes", e);

                    try
                    {
                        tx.Rollback();
                    }
                    catch (Exception ex)
                    {
                        LOGGER.Error("Exception thrown when attempting to roll back nodes update", ex);
                    }
                }
            }
        }
        #endregion

        private IList<NodeInfo> GetGw2Nodes(int worldId, int mapId, DateTime timestamp)
        {
            IList<NodeInfo> nodes = new List<NodeInfo>();

            if (p_WorldIdMap.ContainsKey(worldId) && p_MapIdMap.ContainsKey(mapId))
            {
                string html;

                using (WebClient client = new WebClient())
                {
                    html = client.DownloadString(string.Format("http://gw2nodes.com/index.php?server={0}&map={1}", p_WorldIdMap[worldId], p_MapIdMap[mapId]));
                }

                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                XPathNavigator nav = doc.CreateNavigator();

                XPathNavigator canvas = nav.SelectSingleNode("//div[@id='map']/canvas");
                double width = double.Parse(canvas.GetAttribute("width", null));
                double height = double.Parse(canvas.GetAttribute("height", null));

                XPathNodeIterator imgList = nav.Select("//div[@id='map']/img[@class='node']");
                while (imgList.MoveNext())
                {
                    XPathNavigator img = imgList.Current;

                    string style = img.GetAttribute("style", null);
                    double x = double.Parse(p_XVal.Match(style).Groups[1].Value);
                    double y = double.Parse(p_YVal.Match(style).Groups[1].Value);
                    double w = double.Parse(p_WVal.Match(style).Groups[1].Value);
                    double h = double.Parse(p_HVal.Match(style).Groups[1].Value);

                    string n = img.GetAttribute("nodetype", null);
                    string t = n.ToLower();
                    if (t.Contains("log"))
                        t = "logging";
                    else if (t.Contains("ore"))
                        t = "mining";
                    else
                        t = "harvesting";

                    x = x + (w / 2);
                    y = y + (h / 2);

                    nodes.Add(new NodeInfo()
                        {
                            WorldId = worldId,
                            MapId = mapId,
                            X = (int)x,
                            Y = (int)y,
                            Timestamp = timestamp,
                            Name = n,
                            Type = t
                        });
                }
            }

            return nodes;
        }

        #region Database
        private void DbCreateTables()
        {
            IDbCommand cmd = m_DbConn.CreateCommand();
            IDbTransaction trns = m_DbConn.BeginTransaction();

            cmd.Connection = m_DbConn;
            cmd.Transaction = trns;

            try
            {
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS nodesapi_nodes (
                                        worldid INTEGER,
                                        mapid INTEGER,
                                        x INTEGER,
                                        y INTEGER,
                                        timestamp INTEGER,
                                        name TEXT,
                                        type TEXT,
                                        PRIMARY KEY (worldid, mapid, x, y))";
                cmd.ExecuteNonQuery();

                trns.Commit();
            }
            catch (Exception e)
            {
                LOGGER.Error("Exception thrown when attempting to create tables", e);

                try
                {
                    trns.Rollback();
                }
                catch (Exception ex)
                {
                    LOGGER.Error("Exception thrown when attempting to roll back table creation", ex);
                }
            }
        }
        
        private IList<NodeInfo> DbGetNodes(int worldId, int mapId)
        {
            IList<NodeInfo> nodes = new List<NodeInfo>();

            IDbCommand cmd = m_DbConn.CreateCommand();

            try
            {
                cmd.CommandText = @"SELECT * FROM nodesapi_nodes
                                        WHERE worldid = @worldid AND mapid = @mapid";
                cmd.AddParameter("@worldid", worldId);
                cmd.AddParameter("@mapid", mapId);
                using (IDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        nodes.Add(new NodeInfo()
                            {
                                WorldId = int.Parse(reader["worldid"].ToString()),
                                MapId = int.Parse(reader["mapid"].ToString()),
                                X = int.Parse(reader["x"].ToString()),
                                Y = int.Parse(reader["y"].ToString()),
                                Timestamp = new DateTime(long.Parse(reader["timestamp"].ToString())),
                                Name = reader["name"].ToString(),
                                Type = reader["type"].ToString()
                            });
                    }
                }
            }
            catch (Exception e)
            {
                LOGGER.Error("Exception thrown when attempting to get node data", e);
            }

            return nodes;
        }

        private void DbSaveNode(NodeInfo node, IDbTransaction tx = null)
        {
            IDbCommand cmd = m_DbConn.CreateCommand();

            if (tx != null)
            {
                cmd.Connection = m_DbConn;
                cmd.Transaction = tx;
            }

            try
            {
                cmd.CommandText = @"INSERT OR REPLACE INTO nodesapi_nodes (worldid, mapid, x, y, timestamp, name, type)
                                        VALUES (@worldid, @mapid, @x, @y, @timestamp, @name, @type)";
                cmd.AddParameter("@worldid", node.WorldId);
                cmd.AddParameter("@mapid", node.MapId);
                cmd.AddParameter("@x", node.X);
                cmd.AddParameter("@y", node.Y);
                cmd.AddParameter("@timestamp", node.Timestamp.Ticks);
                cmd.AddParameter("@name", node.Name);
                cmd.AddParameter("@type", node.Type);
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                LOGGER.Error("Exception thrown when attempting to set node data", e);
            }
        }

        private void DbClearNodes(int worldId = -1, int mapId = -1, IDbTransaction tx = null)
        {
            IDbCommand cmd = m_DbConn.CreateCommand();

            if (tx != null)
            {
                cmd.Connection = m_DbConn;
                cmd.Transaction = tx;
            }

            try
            {
                if (worldId < 0 && mapId < 0)
                {
                    cmd.CommandText = @"DELETE FROM nodesapi_nodes WHERE worldid = @worldid AND mapid = @mapid";
                    cmd.AddParameter("@worldid", worldId);
                    cmd.AddParameter("@mapid", mapId);
                }
                else if (worldId < 0)
                {
                    cmd.CommandText = @"DELETE FROM nodesapi_nodes WHERE worldid = @worldid";
                    cmd.AddParameter("@worldid", worldId);
                }
                else if (mapId < 0)
                {
                    cmd.CommandText = @"DELETE FROM nodesapi_nodes WHERE mapid = @mapid";
                    cmd.AddParameter("@mapid", mapId);
                }
                else
                {
                    cmd.CommandText = @"DELETE FROM nodesapi_nodes";
                }

                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                LOGGER.Error("Exception thrown when attempting to clear node data", e);
            }
        }

        private DateTime DbGetLatestTimestamp(int worldId, int mapId)
        {
            IDbCommand cmd = m_DbConn.CreateCommand();

            try
            {
                cmd.CommandText = @"SELECT MAX(timestamp) FROM nodesapi_nodes
                                        WHERE worldid = @worldid AND mapid = @mapid";
                cmd.AddParameter("@worldid", worldId);
                cmd.AddParameter("@mapid", mapId);
                object obj = cmd.ExecuteScalar();
                if (obj != null && !string.IsNullOrEmpty(obj.ToString()))
                    return new DateTime(long.Parse(obj.ToString()));
            }
            catch (Exception e)
            {
                LOGGER.Error(string.Format("Exception thrown when attempting to get latest timestamp for [{0}]", worldId), e);
            }

            return DateTime.MinValue;
        }
        #endregion

        private struct NodeInfo
        {
            public int WorldId;
            public int MapId;
            public int X;
            public int Y;
            public DateTime Timestamp;
            public string Name;
            public string Type;
        }
    }
}
