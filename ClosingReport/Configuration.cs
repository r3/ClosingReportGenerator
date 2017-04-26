using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;

namespace ClosingReport
{
    public class AccountsConfiguration : ConfigurationSection
    {
        private static ConfigurationPropertyCollection properties;
        private static ConfigurationProperty propAccounts;

        static AccountsConfiguration()
        {
            propAccounts = new ConfigurationProperty(null,
                                                    typeof(AccountsElementCollection),
                                                    null,
                                                    ConfigurationPropertyOptions.IsDefaultCollection);
            properties = new ConfigurationPropertyCollection { propAccounts };
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return properties;
            }
        }

        public AccountsElementCollection Accounts
        {
            get
            {
                return this[propAccounts] as AccountsElementCollection;
            }
        }
    }

    public class AccountsElementCollection : ConfigurationElementCollection
    {
        public AccountsElementCollection()
        {
            properties = new ConfigurationPropertyCollection();
        }

        private static ConfigurationPropertyCollection properties;

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return properties;
            }
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }

        protected override string ElementName
        {
            get
            {
                return "account";
            }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new AccountsElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var elm = element as AccountsElement;
            if (elm == null) throw new ArgumentNullException();
            return elm.AccountName;
        }
    }

    public class AccountsElement : ConfigurationElement
    {
        private static ConfigurationPropertyCollection properties;
        private static ConfigurationProperty propAccount;
        private static ConfigurationProperty propCodes;

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return properties;
            }
        }

        public AccountsElement()
        {
            propAccount = new ConfigurationProperty("name", typeof(string), null, ConfigurationPropertyOptions.IsKey);
            propCodes = new ConfigurationProperty("codes", typeof(string), null, ConfigurationPropertyOptions.IsKey);
            properties = new ConfigurationPropertyCollection { propAccount, propCodes };
        }

        public AccountsElement(string accountName)
            : this()
        {
            AccountName = accountName;
        }

        public string AccountName
        {
            get
            {
                return this[propAccount] as string;
            }
            set
            {
                this[propAccount] = value;
            }
        }

        public int[] AccountCodes
        {
            get
            {
                string codes = this[propCodes] as string;
                try
                {
                    return codes.Split(',').Select(x => Convert.ToInt32(x)).ToArray();
                }
                catch (FormatException)
                {
                    ClosingReport.log.TraceEvent(TraceEventType.Warning, 1, $"Unable to parse codes from '{codes}', using: {ClosingReport.sentinel}");
                    return new int[] { ClosingReport.sentinel };
                }
            }
        }
    }

    public class ResourcesConfiguration : ConfigurationSection
    {
        private static ConfigurationPropertyCollection properties;
        private static ConfigurationProperty propResources;

        static ResourcesConfiguration()
        {
            propResources = new ConfigurationProperty(null,
                                                      typeof(ResourcesElementCollection),
                                                      null,
                                                      ConfigurationPropertyOptions.IsDefaultCollection);
            properties = new ConfigurationPropertyCollection { propResources };
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return properties;
            }
        }

        public ResourcesElementCollection Resources
        {
            get
            {
                return this[propResources] as ResourcesElementCollection;
            }
        }
    }

    public class ResourcesElementCollection : ConfigurationElementCollection
    {
        public ResourcesElementCollection()
        {
            properties = new ConfigurationPropertyCollection();
        }

        private static ConfigurationPropertyCollection properties;

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return properties;
            }
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }

        protected override string ElementName
        {
            get
            {
                return "resource";
            }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new ResourceElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var elm = element as ResourceElement;
            if (elm == null) throw new ArgumentNullException();
            return elm.ResourcePath;
        }
    }

    public class ResourceElement : ConfigurationElement
    {
        private static ConfigurationPropertyCollection properties;
        private static ConfigurationProperty propPath;
        private static ConfigurationProperty propDirection;
        private static ConfigurationProperty propReceived;
        private static List<string> inboundIndicators = new List<string>() { "in", "inbound" };

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return properties;
            }
        }

        public ResourceElement()
        {
            propPath = new ConfigurationProperty("path", typeof(string), null, ConfigurationPropertyOptions.IsKey);
            propDirection = new ConfigurationProperty("direction", typeof(string), null, ConfigurationPropertyOptions.IsKey);
            propReceived = new ConfigurationProperty("received", typeof(string), null, ConfigurationPropertyOptions.IsKey);
            properties = new ConfigurationPropertyCollection { propPath, propDirection, propReceived };
        }

        public ResourceElement(string resourcePath)
            : this()
        {
            ResourcePath = resourcePath;
        }

        public string ResourcePath
        {
            get
            {
                return this[propPath] as string;
            }
            set
            {
                this[propPath] = value;
            }
        }

        public CommDirection ResourceDirection
        {
            get
            {
                if (ResourceElement.inboundIndicators.Contains<string>(this[propDirection] as string))
                {
                    return CommDirection.Inbound;
                }

                return CommDirection.Outbound;
            }
            set
            {
                this[propDirection] = value;
            }
        }

        public bool ResourceReceived
        {
            get
            {
                bool result;
                if (bool.TryParse(this[propReceived] as string, out result))
                {
                    return result;
                }

                return false;
            }
            set
            {
                this[propReceived] = value;
            }
        }
    }
}
