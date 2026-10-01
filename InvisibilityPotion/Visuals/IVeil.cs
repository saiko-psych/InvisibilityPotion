namespace InvisibilityPotion.Visuals
{
    public interface IVeil
    {
        /// <param name="forceHide">Draw nothing (remote player hidden from players).</param>
        void Apply(Player p, int tier, bool isLocal, bool forceHide);
        void Remove(Player p);
        /// <summary>Drops the records of destroyed players.</summary>
        void PruneDead();
    }
}
