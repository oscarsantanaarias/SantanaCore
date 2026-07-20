
namespace Santana
{
  internal enum PlayerSetting
  {
    AllowCombiInvite,
    AllowFriendRequest,
    AllowRoomInvite,
    AllowInfoRequest
  }

  public enum GameStartState
  {
    Waiting,
    Loading,
    Countdown,
    ReadyToStart,
    Playing
  }

  internal enum GameRuleState
  {
    Waiting,
    Playing,
    EnteringResult,
    Result,

    FirstHalf,
    EnteringHalfTime,
    HalfTime,
    SecondHalf,
    FullGame,

    Preparing
  }

  internal enum GameRuleStateTrigger
  {
    StartPrepare,
    StartGame,
    EndGame,
    StartResult,
    StartHalfTime,
    StartSecondHalf
  }
}
