﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpHound.OutputObjects
{
    class ACLInfo
    {
        public string ObjectName { get; set; }
        public string ObjectType { get; set; }
        public string PrincipalName { get; set; }
        public string PrincipalType { get; set; }
        public string RightName { get; set; }
        public string AceType { get; set; }
        public string Qualifier { get; set; }
        public bool Inherited { get; set; }

        public string ToCSV()
        {
            return $"{ObjectName},{ObjectType},{PrincipalName},{PrincipalType},{RightName},{AceType},{Qualifier},{Inherited}";
        }

        internal object ToParam()
        {
            return new
            {
                account = ObjectName,
                principal = PrincipalName,

            };
        }
    }
}
