using GameFramework;
using UnityEngine;
public class PlayerCtrlFormData
{
    /// <summary>
    /// �ͷ��չ�/����
    /// </summary>
    public GameFrameworkAction<int> OnClickSkillAtk {
        get;
        set;
    }
    public GameFrameworkAction OnClickHead {
        get;
        set;
    }
    /// <summary>
    /// ��ѯ�Ƿ�����ͷż���
    /// ������/�����ͷż���ʱ��Ϊfalse
    /// </summary>
    public GameFrameworkFunc<bool> OnClickCanRls {
        get;
        set;
    }
    public GameFrameworkAction<Vector2> OnPlayerMove { get; set; }
}
