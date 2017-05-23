﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Net.NetworkInformation;
using System.Security.Principal;

namespace SharpHound
{
    public class Helpers
    {
        static Helpers instance;

        ConcurrentDictionary<string, Domain> DomainResolveCache;
        ConcurrentDictionary<string, string> SidConversionCache;
        ConcurrentDictionary<string, bool> PingCache;
        List<String> DomainList;

        static Options options;

        public static ConcurrentDictionary<string, string> DomainMap = new ConcurrentDictionary<string, string>();

        public static void CreateInstance(Options cli)
        {
            instance = new Helpers(cli);
            string file = options.InMemory ? null : options.DBName;
            DBManager.CreateInstance(file);
        }

        public static Helpers Instance
        {
            get
            {
                return instance;
            }
        }

        public Options Options
        {
            get
            {
                return options;
            }
        }

        public Helpers(Options cli)
        {
            DomainResolveCache = new ConcurrentDictionary<string, Domain>();
            SidConversionCache = new ConcurrentDictionary<string, string>();
            PingCache = new ConcurrentDictionary<string, bool>();
            DomainList = null;
            options = cli;
        }

        public static string DomainFromDN(string dn)
        {
            return dn.Substring(dn.IndexOf("DC=", StringComparison.CurrentCulture)).Replace("DC=", "").Replace(",", ".");
        }

        public DirectorySearcher GetDomainSearcher(string Domain = null, string SearchBase = null, string ADSPath = null)
        {
            Domain TargetDomain = GetDomain(Domain);
            if (TargetDomain == null)
            {
                return null;
            }

            string SearchString;

            if (ADSPath == null)
            {
                string DomainName = TargetDomain.Name;
                string Server = TargetDomain.PdcRoleOwner.Name;
                SearchString = $"LDAP://{Server}/";
                if (SearchBase != null)
                {
                    SearchString += SearchBase;
                }
                else
                {
                    string DomainDN = DomainName.Replace(".", ",DC=");
                    SearchString += "DC=" + DomainDN;
                }
            }
            else
            {
                SearchString = ADSPath;
            }


            options.WriteVerbose(String.Format("[GetDomainSearcher] Search String: {0}", SearchString));

            DirectorySearcher Searcher = new DirectorySearcher(new DirectoryEntry(SearchString))
            {
                PageSize = 1000,
                SearchScope = SearchScope.Subtree,
                CacheResults = false,
                ReferralChasing = ReferralChasingOption.All
            };
            return Searcher;
        }

        public List<string> GetDomainList()
        {
            List<string> Domains = new List<string>();
            if (options.SearchForest)
            {
                Domains = GetForestDomains();
            }
            else if (options.Domain != null)
            {
                Domains.Add(GetDomain(options.Domain).Name);
            }
            else
            {
                Domains.Add(GetDomain().Name);
            }

            return Domains;
        }

        public List<String> GetForestDomains(string Forest = null)
        {
            if (DomainList != null)
            {
                return DomainList;
            }
            Forest f = null;
            List<String> domains = new List<String>();

            if (Forest == null)
            {
                f = System.DirectoryServices.ActiveDirectory.Forest.GetCurrentForest();
            }
            else
            {
                DirectoryContext context = new DirectoryContext(DirectoryContextType.Forest, Forest);
                try
                {
                    f = System.DirectoryServices.ActiveDirectory.Forest.GetForest(context);

                }
                catch
                {
                    return domains;
                }
            }

            foreach (var d in f.Domains)
            {
                domains.Add(d.ToString());
            }

            DomainList = domains;

            return domains;
        }

        public Domain GetDomain(string Domain = null)
        {
            Domain DomainObject;
            //Check if we've already resolved this domain before. If we have return the cached object
            string key = Domain ?? "UNIQUENULLOBJECT";
            if (DomainResolveCache.ContainsKey(key))
            {
                DomainResolveCache.TryGetValue(key, out Domain t);
                return t;
            }

            if (Domain == null)
            {
                try
                {
                    DomainObject = System.DirectoryServices.ActiveDirectory.Domain.GetCurrentDomain();
                }
                catch
                {
                    Console.WriteLine(String.Format("The specified domain {0} does not exist, could not be contacted, or there isn't an existing trust.", Domain));
                    DomainObject = null;
                }
            }
            else
            {
                try
                {
                    DirectoryContext dc = new DirectoryContext(DirectoryContextType.Domain, Domain);
                    DomainObject = System.DirectoryServices.ActiveDirectory.Domain.GetDomain(dc);
                }
                catch
                {
                    Console.WriteLine(String.Format("The specified domain {0} does not exist, could not be contacted, or there isn't an existing trust.", Domain));
                    DomainObject = null;
                }
            }

            if (Domain == null)
            {
                DomainResolveCache.TryAdd("UNIQUENULLOBJECT", DomainObject);
            }
            else
            {
                DomainResolveCache.TryAdd(Domain, DomainObject);
            }
            return DomainObject;
        }

        public string ConvertSIDToName(string cn)
        {
            string TrimmedCN = cn.Trim('*');
            if (SidConversionCache.TryGetValue(TrimmedCN, out string result))
            {
                return result;
            }

            switch (TrimmedCN)
            {
                case "S-1-0":
                    result = "Null Authority";
                    break;
                case "S-1-0-0":
                    result = "Nobody";
                    break;
                case "S-1-1":
                    result = "World Authority";
                    break;
                case "S-1-1-0":
                    result = "Everyone";
                    break;
                case "S-1-2":
                    result = "Local Authority";
                    break;
                case "S-1-2-0":
                    result = "Local";
                    break;
                case "S-1-2-1":
                    result = "Console Logon ";
                    break;
                case "S-1-3":
                    result = "Creator Authority";
                    break;
                case "S-1-3-0":
                    result = "Creator Owner";
                    break;
                case "S-1-3-1":
                    result = "Creator Group";
                    break;
                case "S-1-3-2":
                    result = "Creator Owner Server";
                    break;
                case "S-1-3-3":
                    result = "Creator Group Server";
                    break;
                case "S-1-3-4":
                    result = "Owner Rights";
                    break;
                case "S-1-4":
                    result = "Non-unique Authority";
                    break;
                case "S-1-5":
                    result = "NT Authority";
                    break;
                case "S-1-5-1":
                    result = "Dialup";
                    break;
                case "S-1-5-2":
                    result = "Network";
                    break;
                case "S-1-5-3":
                    result = "Batch";
                    break;
                case "S-1-5-4":
                    result = "Interactive";
                    break;
                case "S-1-5-6":
                    result = "Service";
                    break;
                case "S-1-5-7":
                    result = "Anonymous";
                    break;
                case "S-1-5-8":
                    result = "Proxy";
                    break;
                case "S-1-5-9":
                    result = "Enterprise Domain Controllers";
                    break;
                case "S-1-5-10":
                    result = "Principal Self";
                    break;
                case "S-1-5-11":
                    result = "Authenticated Users";
                    break;
                case "S-1-5-12":
                    result = "Restricted Code";
                    break;
                case "S-1-5-13":
                    result = "Terminal Server Users";
                    break;
                case "S-1-5-14":
                    result = "Remote Interactive Logon";
                    break;
                case "S-1-5-15":
                    result = "This Organization";
                    break;
                case "S-1-5-17":
                    result = "This Organization";
                    break;
                case "S-1-5-18":
                    result = "Local System";
                    break;
                case "S-1-5-19":
                    result = "NT Authority";
                    break;
                case "S-1-5-20":
                    result = "NT Authority";
                    break;
                case "S-1-5-80-0":
                    result = "All Services";
                    break;
                case "S-1-5-32-544":
                    result = "BUILTIN\\Administrators";
                    break;
                case "S-1-5-32-545":
                    result = "BUILTIN\\Users";
                    break;
                case "S-1-5-32-546":
                    result = "BUILTIN\\Guests";
                    break;
                case "S-1-5-32-547":
                    result = "BUILTIN\\Power Users";
                    break;
                case "S-1-5-32-548":
                    result = "BUILTIN\\Account Operators";
                    break;
                case "S-1-5-32-549":
                    result = "BUILTIN\\Server Operators";
                    break;
                case "S-1-5-32-550":
                    result = "BUILTIN\\Print Operators";
                    break;
                case "S-1-5-32-551":
                    result = "BUILTIN\\Backup Operators";
                    break;
                case "S-1-5-32-552":
                    result = "BUILTIN\\Replicators";
                    break;
                case "S-1-5-32-554":
                    result = "BUILTIN\\Pre-Windows 2000 Compatible Access";
                    break;
                case "S-1-5-32-555":
                    result = "BUILTIN\\Remote Desktop Users";
                    break;
                case "S-1-5-32-556":
                    result = "BUILTIN\\Network Configuration Operators";
                    break;
                case "S-1-5-32-557":
                    result = "BUILTIN\\Incoming Forest Trust Builders";
                    break;
                case "S-1-5-32-558":
                    result = "BUILTIN\\Performance Monitor Users";
                    break;
                case "S-1-5-32-559":
                    result = "BUILTIN\\Performance Log Users";
                    break;
                case "S-1-5-32-560":
                    result = "BUILTIN\\Windows Authorization Access Group";
                    break;
                case "S-1-5-32-561":
                    result = "BUILTIN\\Terminal Server License Servers";
                    break;
                case "S-1-5-32-562":
                    result = "BUILTIN\\Distributed COM Users";
                    break;
                case "S-1-5-32-569":
                    result = "BUILTIN\\Cryptographic Operators";
                    break;
                case "S-1-5-32-573":
                    result = "BUILTIN\\Event Log Readers";
                    break;
                case "S-1-5-32-574":
                    result = "BUILTIN\\Certificate Service DCOM Access";
                    break;
                case "S-1-5-32-575":
                    result = "BUILTIN\\RDS Remote Access Servers";
                    break;
                case "S-1-5-32-576":
                    result = "BUILTIN\\RDS Endpoint Servers";
                    break;
                case "S-1-5-32-577":
                    result = "BUILTIN\\RDS Management Servers";
                    break;
                case "S-1-5-32-578":
                    result = "BUILTIN\\Hyper-V Administrators";
                    break;
                case "S-1-5-32-579":
                    result = "BUILTIN\\Access Control Assistance Operators";
                    break;
                case "S-1-5-32-580":
                    result = "BUILTIN\\Access Control Assistance Operators";
                    break;
                default:
                    try
                    {
                        SecurityIdentifier identifier = new SecurityIdentifier(TrimmedCN);
                        result = identifier.Translate(typeof(NTAccount)).Value;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        options.WriteVerbose("Invalid SID " + cn);
                        result = null;
                    }

                    break;
            }
            SidConversionCache.TryAdd(TrimmedCN, result);
            return result;
        }

        public string GetDomainSid(string DomainName)
        {
            byte[] domainSid;
            var dContext = new DirectoryContext(DirectoryContextType.Domain, DomainName);
            using (var domain = Domain.GetDomain(dContext))
            {
                using (var dEntry = domain.GetDirectoryEntry())
                {
                    domainSid = dEntry.Properties["objectSid"].Value as byte[];
                    var sid = new SecurityIdentifier(domainSid, 0);
                    return sid.ToString();
                }
            }
        }

        /// <summary>
        /// Checks if a system responds to ping. Returns true if it does.
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        public bool PingHost(string server)
        {
            if (options.SkipPing)
            {
                return true;
            }

            if (PingCache.TryGetValue(server, out bool val))
            {
                return val;
            }
            Ping ping = new Ping();
            try
            {
                PingReply reply = ping.Send(server, options.PingTimeout);

                if (reply.Status == IPStatus.Success)
                {
                    val = true;
                }
                else
                {
                    val = false;
                }
            }
            catch
            {
                val = false;
            }


            PingCache.TryAdd(server, val);
            return val;
        }
    }
}
