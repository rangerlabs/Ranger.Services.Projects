namespace Ranger.Services.Projects
{
    public static class RedisKeys
    {
        public static string TenantDbPassword(string tenantId) => $"DB_PASSWORD_{tenantId}";
        public static string GetTenantId(string hashedApiKey) => $"TENANT_ID_${hashedApiKey}";
    }
}