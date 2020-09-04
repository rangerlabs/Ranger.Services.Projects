namespace Ranger.Services.Projects
{
    public static class RedisKeys
    {
        public static string GetTenantId(string apiKey) => $"GET_TENANT_ID_${apiKey}";
    }
}