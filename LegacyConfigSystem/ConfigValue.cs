namespace CooldownTracker.Config
{
    public class ConfigValue
    {
        protected string name;
        protected object value;

        public string Name
        {
            get { return Name; }
        }

        public object Value
        {
            get { return value; }
        }

        public ConfigValue(string name, object value)
        {
            this.name  = name;
            this.value = value;
        }
    }
}
