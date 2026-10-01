namespace InvisibilityPotion.Visuals
{
    public interface IVeil
    {
        void Apply(Player p, int tier, bool isLocal);
        void Remove(Player p);
    }
}
