using Santana.Database.Auth;

namespace Santana
{
    internal class Account
    {
        public Account(AccountDto dto)
        {
            AccountDto = dto;
            Id = (ulong)dto.Id;
            Username = dto.Username;
            Nickname = dto.Nickname;
            Hwid = dto.Hwid;
            SecurityLevel = (SecurityLevel)dto.SecurityLevel;
        }

        public AccountDto AccountDto { get; }
        public ulong Id { get; }
        public string Username { get; set; }
        public string Nickname { get; set; }
        public string Hwid { get; set; }
        public SecurityLevel SecurityLevel { get; set; }
    }
}
