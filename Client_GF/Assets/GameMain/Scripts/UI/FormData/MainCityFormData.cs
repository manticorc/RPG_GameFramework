using GameFramework;
using UnityEngine;

public class MainCityFormData 
{ 
    /// <summary>
    /// strong��ť�ص���
    /// </summary>
    public GameFrameworkAction OnClickStrong {
        get;
        set;
    }
    /// <summary>
    /// ������ť�ص���
    /// </summary>
    public GameFrameworkAction OnClickFuBen {
        get;
        set;
    }
    /// <summary>
    /// ����ť�ص���
    /// </summary>
    public GameFrameworkAction OnClickTask {
        get;
        set;
    }
    /// <summary>
    /// ����power��ť�ص���
    /// </summary>
    public GameFrameworkAction<int> OnClickBuy {
        get;
        set;
    } 
    /// <summary>
    /// guide��ť�ص���
    /// </summary>
    public GameFrameworkAction<AutoGuideCfg>  OnClickGuide {
        get;
        set;
    }
    /// <summary>
    /// head��ť�ص���
    /// </summary>
    public GameFrameworkAction OnClickHead {
        get;
        set;
    }
    /// <summary>
    /// ���찴ť�ص���
    /// </summary>
    public GameFrameworkAction  OnClickChat {
        get;
        set;
    }
    /// <summary>
    /// ҡ�˻ص���
    /// </summary>
    public GameFrameworkAction<Vector2> OnMoveDir {
        get;
        set;
    }
}
