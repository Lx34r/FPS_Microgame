using UnityEngine;

public class DisplayMessage : MonoBehaviour
{
    [Tooltip("消息内容")]
    [TextArea]
    public string message;
    [Tooltip("显示消息的Prefab")]
    public GameObject messagePrefab;
    [Tooltip("显示消息前延迟")]
    public float delayBeforeShowing;

    float m_InitTime = float.NegativeInfinity;
    bool m_WasDisplayed;
    DisplayMessageManager m_DisplayMessageManager;

    void Start()
    {
        m_InitTime = Time.time;
        m_DisplayMessageManager = FindObjectOfType<DisplayMessageManager>();
        DebugUtility.HandleErrorIfNullFindObject<DisplayMessageManager, DisplayMessage>(m_DisplayMessageManager, this);
    }

    // Update is called once per frame
    void Update()
    {
        if (m_WasDisplayed)
            return;

        if (Time.time - m_InitTime > delayBeforeShowing)
        {
            var messageInstance = Instantiate(messagePrefab, m_DisplayMessageManager.DisplayMessageRect);
            var notification = messageInstance.GetComponent<NotificationToast>();
            if (notification)
            {
                notification.Initialize(message);
            }

            m_WasDisplayed = true;
        }
    }
}
