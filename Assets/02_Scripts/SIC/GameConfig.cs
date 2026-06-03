// 씬 간에도 유지되는 게임 설정 (static이므로 DontDestroyOnLoad 불필요)
public static class GameConfig
{
    public enum BigObjectType { GiantSlime, BlackMage }
    public static BigObjectType SelectedBigObject;
}
