using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dray : MonoBehaviour, IFacingMover, IKeyMaster
{
    public enum eMode { idle, move, attack, transition, knockback }



    [Header("Set in Inspector")]
    public float speed = 5;
    public float attackDuration = 0.25f;  // Продолжительность атаки
                                          // в секундах

    public float attackDelay = 0.5f;      // Задержка между атаками

    public float transitonDelay = 0.5f;   // Задержка перехода между
                                          // комнатами

    public int maxHealth = 10;

    public float knockbackSpeed = 10;
    public float knockbackDuration = 0.25f;
    public float invincibleDuration = 0.5f;

    [Header("Set Dynamically")]
    public int dirHeld = -1;    // Направление, соответствующее
                                // удержаемой клавише
    public int facing = 1;      // Направление движения Дрея
    public eMode mode = eMode.idle;

    public int numKeys = 0;

    public bool invincible = false;


    public bool hasGrappler = false;
    public Vector3 lastSafeLoc;
    public int lastSafeFacing;


    [SerializeField]
    private int _health;

    public int health
    {
        get { return _health; }
        set { _health = value; }
    }

    private float timeAtkDone = 0;
    private float timeAtkNext = 0;

    private float transitionDone = 0;
    private Vector2 transitionPos;

    private float knockbackDone = 0;
    private float invincibleDone = 0;
    private Vector3 knockbackVel;


    private SpriteRenderer sRend;
    private Rigidbody rigid;
    private Animator anim;
    private InRoom inRm;

    public Vector3[] directions = new Vector3[] { Vector3.right, Vector3.up, Vector3.left, Vector3.down };
    private KeyCode[] keys = new KeyCode[] { KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.LeftArrow, KeyCode.DownArrow };





    private void Awake()
    {
        sRend = GetComponent<SpriteRenderer>();
        rigid = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        inRm = GetComponent<InRoom>();
        health = maxHealth;

        lastSafeLoc = transform.position;       // Начальная позиция безопасна.
        lastSafeFacing = facing;
    }

    private void Update()
    {
        // Проверить состояние неуязвимости и необходимость выполнить отбрасывание
        if (invincible && Time.time > invincibleDone) invincible = false;
        sRend.color = invincible ? Color.red : Color.white;
        if (mode == eMode.knockback)
        {
            rigid.velocity = knockbackVel;
            if (Time.time < knockbackDone) return;
        }


        if (mode == eMode.transition)
        {
            rigid.velocity = Vector3.zero;
            anim.speed = 0;
            roomPos = transitionPos;        // Оставить Дрея на месте
            if (Time.time < transitionDone) return;

            // Следующая строка выполнится, только если Time.time >= transitionDone
            mode = eMode.idle;
        }

        // ---- Обработка ввода с клавиатуры и управление режимами eMode ----
        dirHeld = -1;
        for (int i = 0; i < 4; i++)
        {
            if (Input.GetKey(keys[i]))
            {
                dirHeld = i;
            }
        }

        // Нажата клавиша атаки
        if (Input.GetKeyDown(KeyCode.Z) && Time.time >= timeAtkNext)                // a
        {
            mode = eMode.attack;
            timeAtkDone = Time.time + attackDuration;
            timeAtkNext = Time.time + attackDelay;
        }

        // Завершить атаку, если время истекло
        if (Time.time >= timeAtkDone)                                               //b
        {
            mode = eMode.idle;
        }

        // Выбрать правильный режим, если Дрей не атакует
        if (mode != eMode.attack)                                                     //c
        {
            if (dirHeld == -1)
            {
                mode = eMode.idle;
            }
            else
            {
                facing = dirHeld;                                                   //d
                mode = eMode.move;
            }
        }

        // ---- Действия в текущем режиме ----
        Vector3 vel = Vector3.zero;
        switch (mode)
        {
            case eMode.idle:
                anim.CrossFade("Dray_Walk_" + facing, 0);
                anim.speed = 0;
                break;
            case eMode.move:
                vel = directions[dirHeld];
                anim.CrossFade("Dray_Walk_" + facing, 0);
                anim.speed = 1;
                break;
            case eMode.attack:                                                      // e                                     
                anim.CrossFade("Dray_Attack_" + facing, 0);
                anim.speed = 0;
                break;
            default:
                break;
        }


        if (dirHeld > -1)
        {
            vel = directions[dirHeld];
        }

        rigid.velocity = vel * speed;
    }

    void LateUpdate()
    {
        // Получить координаты узла сетки, с размером ячейки
        // в половину единицы, ближайшего к данному персонажу
        Vector2 rPos = GetRoomPosOnGrid(0.5f);  // Размер ячейки в пол-единицы

        // Персонаж находится на плитке с дверью?
        int doorNum;
        for (doorNum = 0; doorNum < 4; doorNum++)
        {
            if (rPos == InRoom.DOORS[doorNum])              // d
            {
                break;
            }
        }
        if (doorNum > 3 || doorNum != facing) return;      // e

        // Перейти в следующую комнату
        Vector2 rm = roomNum;
        switch (doorNum)                                    // f
        {
            case 0:
                rm.x += 1;
                break;

            case 1:
                rm.y += 1;
                break;

            case 2:
                rm.x -= 1;
                break;

            case 3:
                rm.y -= 1;
                break;
        }

        // Проверить, можно ли выполнить переход в комнату rm
        if (rm.x >=0 && rm.x <= InRoom.MAX_RM_X)
        {
            if (rm.y >= 0 && rm.y <= InRoom.MAX_RM_Y)
            {
                roomNum = rm;
                transitionPos = InRoom.DOORS[(doorNum + 2) % 4];
                roomPos = transitionPos;
                mode = eMode.transition;
                lastSafeLoc = transform.position;
                lastSafeFacing = facing;
                transitionDone = Time.time + transitonDelay;
            }
        }


    }

    void OnCollisionEnter(Collision coll)
    {
        if (invincible) return;     // Выйти, если Дрей пока неуязвим

        DamageEffect dEf = coll.gameObject.GetComponent<DamageEffect>();
        if (dEf == null) return;    // Есликомпонет DamaheEffect отсутствует - выйти

        health -= dEf.damage;       // Вычесть величину ущерба из уровня здоровья

        invincible = true;          // Сделять Дрея неуязвимым
        invincibleDone = Time.time + invincibleDuration;

        if (dEf.knockback)
        {
            // Выполнить отбразывание
            // Определить направление отбрасывания
            Vector3 delta = transform.position - coll.transform.position;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                // Отбрасывает по горизонтали
                delta.x = (delta.x > 0) ? 1 : -1;
                delta.y = 0;
            }
            else
            {
                // Отбрасывает по вертикали
                delta.x = 0;
                delta.y = (delta.y > 0) ? 1 : -1;
            }
            // Применить скорость отскока к компоненту Rigidbody
            knockbackVel = delta * knockbackSpeed;
            rigid.velocity = knockbackVel;

            // Установить режим knockback и время прекращения отбрасывания
            mode = eMode.knockback;
            knockbackDone = Time.time + knockbackDuration;
        }
    }
    
    void OnTriggerEnter(Collider colld)
    {
        PickUp pup = colld.GetComponent<PickUp>();
        if (pup == null) return;

        switch (pup.itemType)
        {
            case PickUp.eType.key:
                keyCount++;
                break;
            case PickUp.eType.health:
                health = Mathf.Min(health + 2, maxHealth);
                break;
            case PickUp.eType.grappler:
                hasGrappler = true;
                break;
        }
        Destroy(colld.gameObject);
    }
    
    public void ResetInRoom(int healthLoss = 0)
    {
        transform.position = lastSafeLoc;
        facing = lastSafeFacing;
        health -= healthLoss;

        invincible = true;  // Сделать Дрея неуязвимым
        invincibleDone = Time.time + invincibleDuration;
    }
    
    
    
    // Реализация интерфейса IFacingMover
    public int GetFacing()
    {
        return facing;
    }
    public bool moving
    {
        get { return (mode == eMode.move); }
    }
    public float GetSpeed()
    {
        return speed;
    }
    public float gridMult
    {
        get { return inRm.gridMult; }
    }
    public Vector2 roomPos
    {
        get { return inRm.roomPos; }
        set { inRm.roomPos = value; }
    }
    public Vector2 roomNum
    {
        get { return inRm.roomNum; }
        set { inRm.roomNum = value; }
    }
    public Vector2 GetRoomPosOnGrid(float mult = -1)
    {
        return inRm.GetRoomPosOnGrid(mult);
    }

    // Реализация интерфейса IKeyMaster
    public int keyCount
    {
        get { return numKeys; }
        set { numKeys = value; }
    }
}
