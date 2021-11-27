namespace CooldownTracker.Config
{
    public class BoolValue : ConfigValue
    {
        protected new bool value;

        public new bool Value
        {
            get { return value; }
            set { this.value = value; }
        }

        public BoolValue(string name, bool value)
            : base(name, value)
        {
            this.value = value;
        }

        public override string ToString()
        {
            return value.ToString();
        }
    }
}
