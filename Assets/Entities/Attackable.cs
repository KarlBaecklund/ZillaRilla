using Attacks.Rilla;
using Assets.Enemy.Finite_State_Machines;
using Assets.Enemy.NPCCode;
using Attacks.Zilla;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attackable : MonoBehaviour
{
	[SerializeField] private float _maxHealth;
	[SerializeField] private float _maxShieldHealth;
	[ContextMenuItem("Revive Player", "QuickRevivePlayer")]
	[SerializeField] private float _currentHealth;
	[SerializeField] private float _currentShieldHealth;
	[SerializeField] private float regenerationSpeed = 0.1f;
	[SerializeField] private float regenHealthAmount = 1f;

	[SerializeField] private Animator _animator;
	[SerializeField] private float _iFrames;
	[SerializeField] public Player.Settings.IfPlayer _playerSettings;
	private RillaSlamSettings _rillaSlamSettings;
	private Coroutine c_invincible;
	private Coroutine c_regenerate;

	private bool _shieldAcitve = true;

	private FiniteStateMachine _fsm;
	//private FSMStateType _fsmStateType;
	private NPC _npc;
	private KnockBack _knockBack;

	//private AttackSettings _settings;

	private Player.Scrips.CharacterInput player;

	// UNITY METHODS
	private void Awake()
	{
		_currentHealth = _maxHealth;
		_currentShieldHealth = _maxShieldHealth;
	}
	
	public void Start()
    {
		TryGetComponent<FiniteStateMachine>(out _fsm);
		TryGetComponent<Player.Scrips.CharacterInput>(out player);
		TryGetComponent<NPC>(out _npc);
		TryGetComponent<KnockBack>(out _knockBack);
		if (_npc && _npc.enemyType == EnemyType.BOSS)
			UIManager.Instance.InGameUI.ActivateBossHealthOnUI();
	}
	private void Update()
	{
		if (player != null)
		{
			if (_currentHealth <= 0 && !_playerSettings._isReviving) //changed this to <= from ==, hope it still works
			{
				_animator.SetBool("Dead", true);
				_playerSettings._isReviving = true;
				PlayerManager.Instance.PlayerNeedsReviving(this);
				if (player.GetCharacter() == Player.Scrips.CharacterInput.Character.ZILLA)
				{
					player.LazorButtonPressed = false;
				}

			}
			else
			{
				float healthPercent = _currentHealth / _maxHealth;
				switch (player.GetCharacter())
				{
					case Player.Scrips.CharacterInput.Character.ZILLA:
						UIManager.Instance.UpdateZillaHealthOnUI(healthPercent);
						break;
					case Player.Scrips.CharacterInput.Character.RILLA:
						UIManager.Instance.UpdateRillaHealthOnUI(healthPercent);
						break;
					default:
						break;
				}
			}
		}
		else if (_npc && _npc.enemyType == EnemyType.BOSS)
		{
			FixBossHealth();
			if (_shieldAcitve && _currentShieldHealth < 1)
			{
				transform.GetChild(2).gameObject.SetActive(false);
			}
		}

	}

	// PUBLIC METHODS
	public void EntitiyHit(AttackSettings settings)
	{
		_rillaSlamSettings = null;
		_animator = GetComponent<Animator>();

		if (settings._settingType == AttackSettings.SettingType.SLAM)
		{
			_rillaSlamSettings = settings as RillaSlamSettings;
			if (_fsm != null && _rillaSlamSettings._stun)
				_fsm.EnterState(FSMStateType.STUN);
		}	
		TestToRemoveHealth(settings);
	}
	
	// INTERNAL METHODS
	//Maybe we should change to make the enemy take dmg first and then check if it's lower than 0 they die?? might be nitpicky tho
	private void TestToRemoveHealth(AttackSettings settings)
	{
		TestForKnockback(settings);
		//if not inv and it's not a boss --> die or take dmg
		if (c_invincible == null && _npc != null && _npc.enemyType != EnemyType.BOSS)
		{
			if (_currentHealth <= 0)
			{
				if (_fsm != null && _fsm._currentState.StateType != FSMStateType.DEATH)
				{
					_fsm.EnterState(FSMStateType.DEATH);
				}
			}
			else
			{
				_currentHealth -= (settings._attackDamage * settings._damageMultiplier);
				_animator.SetTrigger("DamageTaken");
				SpawnHitIcon(settings);
				PlayerManager.Instance.AddToPlayerCombo(settings.playerIndex);
				c_invincible = StartCoroutine(InvincibilityFrames());
			}
		}
		//if it's in vuln-state (boss) then zilla lazor will dmg it
		else if (c_invincible == null && _npc != null && _npc.enemyType == EnemyType.BOSS)
		{
			if (_currentShieldHealth <= 0)
			{
				if (_currentHealth <= 0 && _fsm._currentState.StateType != FSMStateType.DEATH)
				{
					_fsm.EnterState(FSMStateType.DEATH);
					_animator.SetTrigger("Dead");
				}
				else
				{
					//Debug.Log("Damage done " + damage + "Current health " + _currentHealth);
					_currentHealth -= (settings._attackDamage * settings._damageMultiplier);
					_animator.SetTrigger("DamageTaken");
					_fsm.EnterState(FSMStateType.IDLE);
				}
			}
			else
			{
				_currentShieldHealth -= (settings._attackDamage * settings._damageMultiplier);
                if (c_regenerate == null)
                {
					c_regenerate = StartCoroutine(RegenShieldHealth());
				}
			}
			SpawnHitIcon(settings);
			c_invincible = StartCoroutine(InvincibilityFrames()); //gave bosses inv-frames as well!            
			PlayerManager.Instance.AddToPlayerCombo(settings.playerIndex);
		}
		else if (c_invincible == null && player != null)
		{
			//Debug.Log("THIS ENEMYS GOT HANDS"); 
			_currentHealth -= (settings._attackDamage * settings._damageMultiplier);
			if(!(player.GetCharacter() == Player.Scrips.CharacterInput.Character.RILLA && _animator.GetBool("RillaSlam")))
				_animator.SetTrigger("DamageTaken");
			c_invincible = StartCoroutine(InvincibilityFrames());
		}
		else if (c_invincible == null && _currentHealth > 0.0f && gameObject.layer == LayerMask.NameToLayer("Destructible"))
		{
			//Debug.Log("Destructible Damaged for " + settings._attackDamage + "HP");
			_currentHealth -= (settings._attackDamage * settings._damageMultiplier);
			c_invincible = StartCoroutine(InvincibilityFrames());
			GetComponent<PlayOneShot>().PlaySound("Impact");
			if (_currentHealth <= 0.0f)
			{
				SendMessage("BuildingDestruction");
			}
		}
    }

	private void SpawnHitIcon(AttackSettings settings)
	{
		UIManager.Instance.SpawnHitIcon(gameObject.transform.position, settings.playerIndex);
	}

	private void TestForKnockback(AttackSettings settings)
	{
		switch (settings.playerIndex)
		{
			case 0:
				if (settings._knockbackStrength > 0 && _knockBack)
				{
					_animator.SetBool("Attack", false);
					_npc._isKnockedBack = true;
					//Debug.Log(_animator.GetCurrentAnimatorStateInfo(1).IsName("Attack"));
					Vector3 direction = gameObject.transform.position - GameManager.Instance._zilla.gameObject.transform.position;
					direction.y = 0.5f;
					_knockBack.ApplyKnockBack((direction).normalized, settings._knockbackStrength, settings._knockbackTime);
				}
				break;
			case 1:
				if (settings._knockbackStrength > 0 && _knockBack)
				{
					_animator.SetBool("Attack", false);
					//Debug.Log("yes");
					Vector3 direction = gameObject.transform.position - GameManager.Instance._rilla.gameObject.transform.position;
					direction.y = 0.5f;
					_knockBack.ApplyKnockBack((direction).normalized, settings._knockbackStrength, settings._knockbackTime);
				}
				break;
			default:
				Debug.LogError("Game Breaking miss happend in attackable!!");
				break;
		}
	}
	public float GetHealthPercent()
	{
		return (_currentHealth / _maxHealth);
	}
	private void QuickRevivePlayer() //used as contextmenuItem
	{
		PlayerManager.Instance.QuickRevivePlayer(this);
	}
    private IEnumerator InvincibilityFrames()
	{
		yield return new WaitForSeconds(_iFrames);
		//_animator.SetBool("DamageTaken", false);
		c_invincible = null;
	}
	#region Heal
	/// <summary>
	/// Primarily used to heal the player, but will heal whatever entity it's this script is attached to
	/// </summary>
	/// <param name="healAmount"> Amount of health recovered </param>
	public void HealPlayer(float healAmount)
	{
		_currentHealth += healAmount;
	}
	public void ResetHealth(float healthResetPercent = 0)
	{
		if (healthResetPercent == 0)
		{
			healthResetPercent = 1;
		}
		CustomFlagOfDeathScript flagOfDeathScript;
		_animator.SetBool("Dead", false);
		if (TryGetComponent<CustomFlagOfDeathScript>(out flagOfDeathScript))
		{
			Debug.Log("Im In");
			flagOfDeathScript.RemoveFlagOfDeath();
		}
		_currentHealth = healthResetPercent * _maxHealth;
	}
	#endregion
	private IEnumerator RegenShieldHealth()
	{
		//Debug.Log("NU K??R VIII");
		//yield return new WaitForSeconds(regenerationSpeed);
		while (_currentShieldHealth < _maxShieldHealth && _currentShieldHealth > 0)
		{
			_currentShieldHealth += regenHealthAmount;
			yield return new WaitForSeconds(regenerationSpeed);
		}
		c_regenerate = null;
	}

	private void FixBossHealth()
	{
		UIManager.Instance.InGameUI.SetHealthOnBossShieldBar(_currentShieldHealth / _maxShieldHealth);
		UIManager.Instance.InGameUI.SetHealthOnBossHealthBar(_currentHealth / _maxHealth );
	}
	private void OnDestroy()
	{
		if (_npc && _npc.enemyType == EnemyType.BOSS)
			UIManager.Instance.InGameUI.DeactivateBossHealthOnUI();
	}
}

namespace Player.Settings
{
	[Serializable]
	public class IfPlayer
	{
		[Header("Revive")]
		public float _timeToRevive;
		public float _timeUntilDeath;
		[HideInInspector] public bool _isReviving;
	}
}
