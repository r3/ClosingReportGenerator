using System;
using System.Configuration;
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
                    return new int[] { -1 };
                }
            }
        }
    }
}
