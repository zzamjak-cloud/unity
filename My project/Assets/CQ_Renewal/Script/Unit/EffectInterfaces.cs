/// <summary>
/// 캐릭터 이펙트 관련 인터페이스들을 정의하는 파일
/// ICharacter로 시작하는 네이밍 컨벤션을 따릅니다.
/// </summary>

/// <summary>
/// 캐릭터의 공격 이펙트를 재생할 수 있는 인터페이스
/// </summary>
public interface ICharacterAttackEffect 
{ 
    void PlayAttackEffect(); 
}

/// <summary>
/// 캐릭터의 이동 이펙트를 재생할 수 있는 인터페이스
/// </summary>
public interface ICharacterMoveEffect 
{ 
    void PlayMoveEffect(bool play); 
}

/// <summary>
/// 캐릭터의 Blank 이펙트를 재생할 수 있는 인터페이스
/// </summary>
public interface ICharacterBlankEffect 
{ 
    void PlayBlankEffect(); 
}

/// <summary>
/// 캐릭터의 피격 이펙트를 재생할 수 있는 인터페이스
/// </summary>
public interface ICharacterDamageEffect 
{ 
    void PlayDamageEffect(); 
}
