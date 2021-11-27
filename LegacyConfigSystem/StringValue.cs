namespace CooldownTracker.Config
{
    public class StringValue : ConfigValue
    {
        protected new string value;

        public new string Value
        {
            get { return value; }
            set { this.value = value; }
        }

        public StringValue(string name, string value)
            : base(name, value)
        {
            this.value = value;
        }

        public override string ToString()
        {
            return value;
        }
    }
}
