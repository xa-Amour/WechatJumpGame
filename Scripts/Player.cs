using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using LeanCloud;
using UniRx;
using Random = UnityEngine.Random;
using System;

public class Player : MonoBehaviour
{
    public float Factor;

    public float MaxDistance = 5;
    public GameObject Stage;

    public Transform Camera;

    public Text ScoreText;

    public GameObject Particle;

    public Transform Head;
    public Transform Body;

    public Text SingleScoreText;

    public GameObject SaveScorePanel;
    public InputField NameFile;
    public Button SaveButton;

    public GameObject RankPanel;
    public GameObject RankItem;

    public Button RestartButton;


    private Rigidbody _rigidbody;
    private float _startTime;
    private GameObject _currentStage;
    private Collider _lastCollisiomCollider;
    private Vector3 _cameraRelativePosition;
    private int _score;
    private bool _isUpdateScoreAnimation = false;

    Vector3 _direction = new Vector3(1, 0, 0);
    private float _scoreAnimationStartTime;


    // Use this for initialization
    void Start () {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.centerOfMass = Vector3.zero;
        _currentStage = Stage;
        _lastCollisiomCollider = _currentStage.GetComponent<Collider>();
        SpawnStage();

        _cameraRelativePosition = Camera.position - transform.position;

        SaveButton.onClick.AddListener(OnClickSaveButton);
        RestartButton.onClick.AddListener(() => 
        {
            SceneManager.LoadScene(0);
        });

        MainThreadDispatcher.Initialize();
    }
	
	// Update is called once per frame
	void Update (){

        //按下空格一瞬间
        //if (Input.GetKeyDown(KeyCode.Space))
        if (Input.touches[0].phase == TouchPhase.Began)
        {
            _startTime = Time.time;
            Particle.SetActive(true);
        }
        //松开空格一瞬间
        //if (Input.GetKeyUp(KeyCode.Space))
        if (Input.touches[0].phase == TouchPhase.Ended)
        {
            var elapse = Time.time - _startTime;
             OnJump(elapse);
            Particle.SetActive(false);

            Body.transform.DOScale(0.1f, 0.2f);
            Head.transform.DOLocalMoveY(0.29f, 0.2f);

            _currentStage.transform.DOLocalMoveY(0.25F, 0.2f);
            _currentStage.transform.DOScale(new Vector3(1, 0.5f,1),0.2f);

        }
        //按住空格每一帧
        //if (Input.GetKey(KeyCode.Space))
        if (Input.touches[0].phase == TouchPhase.Stationary)
        {
            Debug.Log(Time.deltaTime);
            if (_currentStage.transform.localScale.y > 0.3)
            {
                Body.transform.localScale += new Vector3(1, -1, 1) * 0.05f * Time.deltaTime;
                Head.transform.localPosition += new Vector3(0, -1, 0) * 0.1f * Time.deltaTime;
                _currentStage.transform.localScale += new Vector3(0, -1, 0) * 0.15f * Time.deltaTime;
                _currentStage.transform.localPosition += new Vector3(0, -1, 0) * 0.15f * Time.deltaTime;
            }
        }
        if (_isUpdateScoreAnimation)
        {
            UpdateScoreAnimation();
        }

	}
    //计算跳多远的方法
    void OnJump(float elapse)
    {
        _rigidbody.AddForce((new Vector3(0, 1, 0) + _direction) * elapse * Factor,ForceMode.Impulse);
    }

    //复制下一个盒子
    void SpawnStage()
    {
        var stage = Instantiate(Stage);
        stage.transform.position = _currentStage.transform.position + _direction * Random.Range(1.1f, MaxDistance);

        var randomScale = Random.Range(0.5f, 1);
        stage.transform.localScale = new Vector3(randomScale, 0.5f, randomScale);

        stage.GetComponent<Renderer>().material.color = new Color(Random.Range(0f, 1), Random.Range(0f, 1), Random.Range(0f, 1));

    }

    //判断小人是否跳上盒子等逻辑
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log(collision.gameObject.name);

        if (collision.gameObject.name.Contains("Stage") && collision.collider != _lastCollisiomCollider)
        {
            _lastCollisiomCollider = collision.collider;
            _currentStage = collision.gameObject;
            RandomDirection();

            SpawnStage();
            MoveCamera();
            ShowScoreAnimation();

            _score++;
            ScoreText.text = _score.ToString();
        }

        if (collision.gameObject.name == "Ground")
        {
            //本局游戏结束，显示上传的分数panel
            SaveScorePanel.SetActive(true);
            _rigidbody.useGravity = false;

            //Destroy(_rigidbody.GetComponent<Rigidbody>());
            //_rigidbody.Sleep();
            //_rigidbody.constraints = RigidbodyConstraints.FreezePositionX;
            //_rigidbody.constraints = RigidbodyConstraints.FreezePositionY;
            //_rigidbody.constraints = RigidbodyConstraints.FreezePositionZ;

        }
    }


    //显示飘分动画（+1的效果）
    private void ShowScoreAnimation()
    {
        _isUpdateScoreAnimation = true;
        _scoreAnimationStartTime = Time.time;
    }

    //更新分数的动画效果
    void UpdateScoreAnimation()
    {
        if (Time.time - _scoreAnimationStartTime > 1)
            _isUpdateScoreAnimation = false;
        var playerScreenPos = RectTransformUtility.WorldToScreenPoint(Camera.GetComponent<Camera>(), transform.position);
        SingleScoreText.transform.position = playerScreenPos + Vector2.Lerp(Vector2.zero, new Vector2(0, 200),
            Time.time - _scoreAnimationStartTime);

        SingleScoreText.color = Color.Lerp(Color.black, new Color(0, 0, 0, 0), Time.time - _scoreAnimationStartTime);
    }

    //随机产生下一个盒子的位置方向（即x方向或z方向）
    void RandomDirection()
    {
        var seed = Random.Range(0, 2);
        if (seed == 0)
        {
            _direction = new Vector3(1, 0, 0);
        }
        else
        {
            _direction = new Vector3(0, 0, 1);
        }
    }

    //摄像头相对位置
    void MoveCamera()
    {
        Camera.DOMove(transform.position + _cameraRelativePosition, 1);
    }

    //保存玩家分数
    void OnClickSaveButton()
    {
        var nickname = NameFile.text;
        AVObject gameScore = new AVObject("GameScore");
        gameScore["score"] = _score;
        gameScore["playerName"] = nickname;
        gameScore.SaveAsync().ContinueWith(_ =>
        {
            ShowRankPanel();
        });
        SaveScorePanel.SetActive(false);
    }

    //显示玩家分数排行
    void ShowRankPanel()
    {
        AVQuery<AVObject> query = new AVQuery<AVObject>("GameScore").OrderByDescending("score").Limit(10);
        query.FindAsync().ContinueWith(t =>
        {
            var results = t.Result;
            var scores = new List<string>();

            foreach (var result in results)
            {
                var score = result["playerName"] + ":" + result["score"];
                scores.Add(score);
            }

            MainThreadDispatcher.Send(_ =>
            {
                foreach (var score in scores)
                {
                    var item = Instantiate(RankItem);
                    item.SetActive(true);
                    item.GetComponent<Text>().text = score;
                    item.transform.SetParent(RankItem.transform.parent);            
                }
                RankPanel.SetActive(true);
            }, null);
        });
    }
}
