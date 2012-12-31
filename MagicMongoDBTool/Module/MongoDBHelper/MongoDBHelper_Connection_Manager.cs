﻿using System;
using System.Collections.Generic;
using MongoDB.Driver;

namespace MagicMongoDBTool.Module
{
    public static partial class MongoDBHelper
    {
        #region"服务器管理"
        /// <summary>
        /// 管理中服务器列表
        /// </summary>
        public static Dictionary<String, MongoServer> _mongoConnSvrLst = new Dictionary<String, MongoServer>();

        public static Dictionary<String, MongoServerInstance> _mongoInstanceLst = new Dictionary<String, MongoServerInstance>();

        /// <summary>
        /// 增加管理服务器
        /// </summary>
        /// <param name="configLst"></param>
        /// <returns></returns>
        public static void AddServer(List<ConfigHelper.MongoConnectionConfig> configLst)
        {
            for (int i = 0; i < configLst.Count; i++)
            {
                ConfigHelper.MongoConnectionConfig config = configLst[i];
                try
                {
                    if (_mongoConnSvrLst.ContainsKey(config.ConnectionName))
                    {
                        _mongoConnSvrLst.Remove(config.ConnectionName);
                    }
                    _mongoConnSvrLst.Add(config.ConnectionName, CreateMongoSetting(ref config));
                    ///更新一些运行时的变量
                    SystemManager.ConfigHelperInstance.ConnectionList[config.ConnectionName] = config;
                }
                catch (Exception ex)
                {
                    MyMessageBox.ShowMessage("Exception", "Can't Connect to Server：" + config.ConnectionName, ex.ToString(), true);
                }
            }
        }
        /// <summary>
        /// 根据config获得Server,同时更新一些运行时变量
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static MongoServer CreateMongoSetting(ref ConfigHelper.MongoConnectionConfig config)
        {

            MongoServerSettings mongoSvrSetting = new MongoServerSettings();
            if (String.IsNullOrEmpty(config.ConnectionString))
            {
                mongoSvrSetting.ConnectionMode = ConnectionMode.Direct;
                SetReadPreferenceWriteConcern(ref mongoSvrSetting, config);
                //Replset时候可以不用设置吗？                    
                mongoSvrSetting.Server = new MongoServerAddress(config.Host, config.Port);
                //MapReduce的时候将消耗大量时间。不过这里需要平衡一下，太长容易造成并发问题
                //From Driver 1.4 Pay attention to this comment
                //The default value for SocketTimeout has been changed from 30 seconds to 0, 
                //which is a special value meaning to use the operating system default value, 
                //which in turn is infinity. If you actually want a SocketTimeout you now have to set it yourself. 
                //The SocketTimeout is currently a server level setting, but most likely in a future release it will be possible to set it at other levels, 
                //including for individual operations.
                if (config.socketTimeoutMS != 0)
                {
                    mongoSvrSetting.SocketTimeout = new TimeSpan(0, 0, (int)(config.socketTimeoutMS / 1000));
                }
                if (config.connectTimeoutMS != 0)
                {
                    mongoSvrSetting.ConnectTimeout = new TimeSpan(0, 0, (int)(config.connectTimeoutMS / 1000));
                }
                if (config.wtimeoutMS != 0)
                {
                    mongoSvrSetting.WaitQueueTimeout = new TimeSpan(0, 0, (int)(config.wtimeoutMS / 1000));
                }
                if (config.WaitQueueSize != 0)
                {
                    mongoSvrSetting.WaitQueueSize = config.WaitQueueSize;
                }
                //运行时LoginAsAdmin的设定
                config.LoginAsAdmin = (config.DataBaseName == String.Empty);
                if (!(String.IsNullOrEmpty(config.UserName) || String.IsNullOrEmpty(config.Password)))
                {
                    //认证的设定:注意，这里的密码是明文
                    mongoSvrSetting.DefaultCredentials = new MongoCredentials(config.UserName, config.Password, config.LoginAsAdmin);
                }
                if (config.ReplSetName != String.Empty)
                {
                    mongoSvrSetting.ReplicaSetName = config.ReplSetName;
                    config.ServerRole = ConfigHelper.SvrRoleType.ReplsetSvr;
                }
                else
                {
                    config.ServerRole = ConfigHelper.SvrRoleType.DataSvr;
                }
                if (config.ServerRole == ConfigHelper.SvrRoleType.ReplsetSvr)
                {
                    //ReplsetName不是固有属性,可以设置，不过必须保持与配置文件的一致
                    mongoSvrSetting.ConnectionMode = ConnectionMode.ReplicaSet;
                    //添加Replset服务器，注意，这里可能需要事先初始化副本
                    List<MongoServerAddress> ReplsetSvrList = new List<MongoServerAddress>();
                    foreach (String item in config.ReplsetList)
                    {
                        //如果这里的服务器在启动的时候没有--Replset参数，将会出错，当然作为单体的服务器，启动是没有任何问题的
                        MongoServerAddress ReplSrv;
                        if (item.Split(":".ToCharArray()).Length == 2)
                        {
                            ReplSrv = new MongoServerAddress(
                                            item.Split(":".ToCharArray())[0],
                                            Convert.ToInt16(item.Split(":".ToCharArray())[1]));
                        }
                        else
                        {
                            ReplSrv = new MongoServerAddress(item);
                        }
                        ReplsetSvrList.Add(ReplSrv);
                    }
                    mongoSvrSetting.Servers = ReplsetSvrList;
                }
            }
            else
            {
                //使用MongoConnectionString建立连接
                //mongoSvrSetting = MongoUrl.Create(config.ConnectionString).ToServerSettings();
                mongoSvrSetting = MongoServerSettings.FromUrl(MongoUrl.Create(config.ConnectionString));
            }
            MongoServer masterMongoSvr = new MongoServer(mongoSvrSetting);
            return masterMongoSvr;
        }
        /// <summary>
        /// Set ReadPreference And WriteConcern 
        /// </summary>
        /// <param name="mongoSvrSetting"></param>
        /// <param name="config"></param>
        private static void SetReadPreferenceWriteConcern(ref MongoServerSettings mongoSvrSetting, ConfigHelper.MongoConnectionConfig config)
        {
            //----------------------------------------------
            //            New MongoClient class and default WriteConcern
            //----------------------------------------------

            //The new default WriteConcern is Acknowledged, but we have introduced the new
            //default in a way that doesn't alter the behavior of existing programs. We
            //are introducing a new root class called MongoClient that defaults the 
            //WriteConcern to Acknowledged. The existing MongoServer Create methods are
            //deprecated but when used continue to default to a WriteConcern of Unacknowledged.

            //In prior releases you would start using the C# driver with code like this:

            //    var connectionString = "mongodb://localhost";
            //    var server = MongoServer.Create(connectionString); // deprecated
            //    var database = server.GetDatabase("test"); // WriteConcern defaulted to Unacknowledged

            //The new way to start using the C# driver is:

            //    var connectionString = "mongodb://localhost";
            //    var client = new MongoClient(connectionString);
            //    var server = client.GetServer();
            //    var database = server.GetDatabase("test"); // WriteConcern defaulted to Acknowledged

            //If you use the old way to start using the driver the default WriteConcern will
            //be Unacknowledged, but if you use the new way (using MongoClient) the default
            //WriteConcern will be Acknowledged.

            //当一个服务器作为从属服务器，副本组中的备用服务器，这里一定要设置为SlaveOK,默认情况下是不可以读取的
            //SlaveOK 过时,使用ReadPreference
            if (config.ReadPreference == ReadPreference.Primary.ToString())
            {
                mongoSvrSetting.ReadPreference = ReadPreference.Primary;
            }
            if (config.ReadPreference == ReadPreference.PrimaryPreferred.ToString())
            {
                mongoSvrSetting.ReadPreference = ReadPreference.PrimaryPreferred;
            }
            if (config.ReadPreference == ReadPreference.Secondary.ToString())
            {
                mongoSvrSetting.ReadPreference = ReadPreference.Secondary;
            }
            if (config.ReadPreference == ReadPreference.SecondaryPreferred.ToString())
            {
                mongoSvrSetting.ReadPreference = ReadPreference.SecondaryPreferred;
            }
            if (config.ReadPreference == ReadPreference.Nearest.ToString())
            {
                mongoSvrSetting.ReadPreference = ReadPreference.Nearest;
            }
            //Default ReadPreference is Primary
            //安全模式
            if (config.WriteConcern == WriteConcern.Unacknowledged.ToString())
            {
                mongoSvrSetting.WriteConcern = WriteConcern.Unacknowledged;
            }
            if (config.WriteConcern == WriteConcern.Acknowledged.ToString())
            {
                mongoSvrSetting.WriteConcern = WriteConcern.Acknowledged;
            }
            if (config.WriteConcern == WriteConcern.W2.ToString())
            {
                mongoSvrSetting.WriteConcern = WriteConcern.W2;
            }
            if (config.WriteConcern == WriteConcern.W3.ToString())
            {
                mongoSvrSetting.WriteConcern = WriteConcern.W3;
            }
            if (config.WriteConcern == WriteConcern.W4.ToString())
            {
                mongoSvrSetting.WriteConcern = WriteConcern.W4;
            }
            if (config.WriteConcern == WriteConcern.WMajority.ToString())
            {
                mongoSvrSetting.WriteConcern = WriteConcern.WMajority;
            }
            //Default WriteConcern is w=0
        }
        /// <summary>
        /// 使用字符串连接来填充
        /// </summary>
        /// <remarks>http://www.mongodb.org/display/DOCS/Connections</remarks>
        /// <param name="connectionString"></param>
        /// <param name="config"></param>
        public static String FillConfigWithConnectionString(ref ConfigHelper.MongoConnectionConfig config)
        {
            String connectionString = config.ConnectionString;
            //mongodb://[username:password@]host1[:port1][,host2[:port2],...[,hostN[:portN]]][/[database][?options]]
            try
            {
                MongoUrl mongourl = MongoUrl.Create(connectionString);
                config.DataBaseName = mongourl.DatabaseName;
                if (mongourl.DefaultCredentials != null)
                {
                    config.UserName = mongourl.DefaultCredentials.Username;
                    config.Password = mongourl.DefaultCredentials.Password;
                    config.LoginAsAdmin = mongourl.DefaultCredentials.Admin;
                }
                config.Host = mongourl.Server.Host;
                config.Port = mongourl.Server.Port;
                config.ReadPreference = mongourl.ReadPreference.ToString();
                //TODO: Is this OK??
                config.WriteConcern = mongourl.GetWriteConcern(true).ToString();
                config.socketTimeoutMS = (int)mongourl.SocketTimeout.TotalMilliseconds;
                config.connectTimeoutMS = (int)mongourl.ConnectTimeout.TotalMilliseconds;
                config.wtimeoutMS = (int)mongourl.WaitQueueTimeout.TotalMilliseconds;
                config.WaitQueueSize = (int)mongourl.WaitQueueSize;
                config.ReplSetName = mongourl.ReplicaSetName;
                foreach (var item in mongourl.Servers)
                {
                    config.ReplsetList.Add(item.Host + (item.Port == 0 ? String.Empty : ":" + item.Port.ToString()));
                }
                return String.Empty;
            }
            catch (FormatException ex)
            {
                return ex.ToString();
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
        #endregion
    }
}
