namespace Ranger.Services.Projects
{
    public static class RedisKeys
    {
        public static string TenantDbPassword(string tenantId) => $"DB_PASSWORD_{tenantId}";
    }
}