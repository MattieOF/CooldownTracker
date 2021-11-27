namespace CooldownTracker.Config
{
    public class IntValue : ConfigValue
    {
        protected new int value;

        public new int Value
        {
            get { return value; }
            set { this.value = value; }
        }

        public IntValue(string name, int value)
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
