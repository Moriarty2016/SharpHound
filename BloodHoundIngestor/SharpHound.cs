﻿using CommandLine;
using CommandLine.Text;
using System;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Security.Permissions;

namespace SharpHound
{
    public class Options
    {
        public enum CollectionMethod{
            Group,
            ComputerOnly,
            LocalGroup,
            GPOLocalGroup,
            Session,
            LoggedOn,
            Trusts,
            Stealth,
            Default
        }

        [Option('c', "CollectionMethod", DefaultValue = CollectionMethod.Default, HelpText = "Collection Method (Group, LocalGroup, GPOLocalGroup, Session, LoggedOn, ComputerOnly, Trusts, Stealth, Default")]
        public CollectionMethod CollMethod { get; set; }

        [Option('v',"Verbose", DefaultValue=false, HelpText="Enables Verbose Output")]
        public bool Verbose { get; set; }

        [Option('t',"Threads", DefaultValue = 20, HelpText ="Set Number of Enumeration Threads")]
        public int Threads { get; set; }

        [Option('f',"CSVFolder", DefaultValue = ".", HelpText ="Set the directory to output CSV Files")]
        public string CSVFolder { get; set; }

        [Option('p',"CSVPrefix", DefaultValue = "", HelpText ="Set the prefix for the CSV files")]
        public string CSVPrefix { get; set; }

        [Option('d', "Domain", MutuallyExclusiveSet ="domain", DefaultValue = null, HelpText = "Domain to enumerate")]
        public string Domain { get; set; }

        [Option('s',"SearchForest", MutuallyExclusiveSet ="domain", DefaultValue =null, HelpText ="Enumerate entire forest")]
        public bool SearchForest { get; set; }

        [Option("URI", DefaultValue = null, HelpText ="URI for Neo4j Rest API")]
        public string URI { get; set; }

        [Option("UserPass", DefaultValue = null, HelpText ="username:password for the Neo4j Rest API")]
        public string UserPass { get; set; }

        [Option("SkipGCDeconfliction",DefaultValue =false,HelpText ="Skip Global Catalog Deconfliction for Sessions")]
        public bool SkipGCDeconfliction { get; set; }

        [Option("SkipPing",DefaultValue =false,HelpText ="Skip ping checks on computer enumeration")]
        public bool SkipPing { get; set; }

        [Option("PingTimeout", DefaultValue = 750,HelpText ="Timeout in Milliseconds for Ping Checks")]
        public int PingTimeout { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }

        public void WriteVerbose(string Message)
        {
            if (Verbose)
            {
                Console.WriteLine(Message);
            }
        }

        public string GetFilePath(string filename)
        {
            string f;
            if (CSVPrefix.Equals(""))
            {
                f = filename;
            }else
            {
                f = CSVPrefix + "_" + filename;
            }
            return Path.Combine(CSVFolder, f);
        }
    }
    class BloodHoundIngestor
    {
        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
        static void Main(string[] args)
        {
            var options = new Options();

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(MyHandler);
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                Helpers.CreateInstance(options);
                
                Domain d = Helpers.Instance.GetDomain(options.Domain);
                if (d == null)
                {
                    Console.WriteLine("Unable to contact domain or invalid domain specified");
                    Environment.Exit(0);
                }

                if (options.CollMethod.Equals(Options.CollectionMethod.Default))
                {
                    DomainTrustMapping TrustMapper = new DomainTrustMapping();
                    TrustMapper.GetDomainTrusts();
                    DomainGroupEnumeration GroupEnumeration = new DomainGroupEnumeration();
                    GroupEnumeration.EnumerateGroupMembership();
                    LocalAdminEnumeration AdminEnumeration = new LocalAdminEnumeration();
                    AdminEnumeration.EnumerateLocalAdmins();
                    SessionEnumeration SessionEnum = new SessionEnumeration();
                    SessionEnum.EnumerateSessions();
                }
                else if (options.CollMethod.Equals(Options.CollectionMethod.Trusts))
                {
                    DomainTrustMapping TrustMapper = new DomainTrustMapping();
                    TrustMapper.GetDomainTrusts();
                }else if (options.CollMethod.Equals(Options.CollectionMethod.LocalGroup))
                {
                    LocalAdminEnumeration AdminEnumeration = new LocalAdminEnumeration();
                    AdminEnumeration.EnumerateLocalAdmins();
                }else if (options.CollMethod.Equals(Options.CollectionMethod.Group))
                {
                    DomainGroupEnumeration GroupEnumeration = new DomainGroupEnumeration();
                    GroupEnumeration.EnumerateGroupMembership();
                }else if (options.CollMethod.Equals(Options.CollectionMethod.Session))
                {
                    SessionEnumeration SessionEnum = new SessionEnumeration();
                    SessionEnum.EnumerateSessions();
                }else if (options.CollMethod.Equals(Options.CollectionMethod.Stealth))
                {
                    SessionEnumeration SessionEnum = new SessionEnumeration();
                    SessionEnum.EnumerateSessions();
                }
            }
            
        }

        static void MyHandler(object sender, UnhandledExceptionEventArgs args)
        {
            try
            {
                Exception e = (Exception)args.ExceptionObject;
                Console.WriteLine("MyHandler caught : " + e.Message);
            }
            catch
            {
                Console.WriteLine("Exception logging exception");
                Console.WriteLine(args.ToString());
            }
        }
    }
}