using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Multi_Ttt_Client
{
    public partial class Form1 : Form
    {
        // --- [게임 설정 및 상태] ---
        private enum GameMode { None, LocalMulti, VsAI, ServerMulti }
        private GameMode currentMode = GameMode.None;

        private Button[] btnBoard = new Button[9];
        private int[] board = new int[9]; 
        private Queue<int> historyP1 = new Queue<int>();
        private Queue<int> historyP2 = new Queue<int>();

        private int currentPlayer = 1;
        private bool isGameOver = false;
        private Random aiRandom = new Random();

        public Form1()
        {
            this.Text = "Multi_Ttt - Game Project";
            this.ClientSize = new Size(330, 500);
            this.StartPosition = FormStartPosition.CenterScreen;

            ShowMenu(); // 처음 시작 시 메뉴를 먼저 보여줌
        }

        // --- [기획 1단계: 모드 선택 메뉴] ---
        private void ShowMenu()
        {
            this.Controls.Clear();

            Label lblTitle = new Label { Text = "Multi Ttt", Font = new Font("Arial", 20, FontStyle.Bold), Dock = DockStyle.Top, Height = 60, TextAlign = ContentAlignment.MiddleCenter };
            
            Button btnLocal = new Button { Text = "로컬 멀티플레이", Dock = DockStyle.Top, Height = 50 };
            btnLocal.Click += (s, e) => StartGame(GameMode.LocalMulti);

            Button btnAI = new Button { Text = "AI 대전 (싱글)", Dock = DockStyle.Top, Height = 50 };
            btnAI.Click += (s, e) => StartGame(GameMode.VsAI);

            Button btnServer = new Button { Text = "서버 멀티플레이 (준비 중)", Dock = DockStyle.Top, Height = 50, Enabled = false };

            this.Controls.Add(btnServer);
            this.Controls.Add(btnAI);
            this.Controls.Add(btnLocal);
            this.Controls.Add(lblTitle);
        }

        private void StartGame(GameMode mode)
        {
            currentMode = mode;
            this.Controls.Clear();
            InitializeBoard();
        }

        private void InitializeBoard()
        {
            for (int i = 0; i < 9; i++)
            {
                btnBoard[i] = new Button();
                btnBoard[i].Size = new Size(100, 100);
                btnBoard[i].Location = new Point(10 + (i % 3) * 105, 10 + (i / 3) * 105);
                btnBoard[i].Tag = i;
                btnBoard[i].Font = new Font("Arial", 24, FontStyle.Bold);
                btnBoard[i].Click += OnCellClick;
                this.Controls.Add(btnBoard[i]);
            }

            Button btnBack = new Button { Text = "메뉴로", Dock = DockStyle.Bottom, Height = 40 };
            btnBack.Click += (s, e) => { ResetGameData(); ShowMenu(); };
            this.Controls.Add(btnBack);

            RefreshBoard();
        }

        // --- [기획 2단계: 핵심 플레이 로직] ---
        private void OnCellClick(object sender, EventArgs e)
        {
            if (isGameOver || (currentMode == GameMode.VsAI && currentPlayer == 2)) return;

            Button clickedBtn = (Button)sender;
            int index = (int)clickedBtn.Tag;

            if (board[index] != 0) return;

            ProcessTurn(index);

            // AI 대전 모드이고 게임이 안 끝났다면 AI 턴 실행
            if (currentMode == GameMode.VsAI && !isGameOver && currentPlayer == 2)
            {
                // 잠시 대기 후 AI가 두도록 함 (연출)
                System.Windows.Forms.Timer aiTimer = new System.Windows.Forms.Timer { Interval = 500 };
                aiTimer.Tick += (s, ev) => {
                    aiTimer.Stop();
                    DoAIMove();
                };
                aiTimer.Start();
            }
        }

        private void ProcessTurn(int index)
        {
            var currentHistory = (currentPlayer == 1) ? historyP1 : historyP2;

            if (currentHistory.Count >= 3)
            {
                int oldIndex = currentHistory.Dequeue();
                board[oldIndex] = 0;
            }

            board[index] = currentPlayer;
            currentHistory.Enqueue(index);

            if (CheckWinner(currentPlayer))
            {
                RefreshBoard();
                MessageBox.Show($"플레이어 {currentPlayer} 승리!");
                isGameOver = true;
                return;
            }

            currentPlayer = (currentPlayer == 1) ? 2 : 1;
            RefreshBoard();
        }

        // --- [기획 3단계: AI 로직] ---
        private void DoAIMove()
        {
            // 1. [공격] 내가 한 줄을 완성할 수 있는가?
            int winMove = FindBestMove(2); // AI가 2번 플레이어
            if (winMove != -1 && aiRandom.Next(100) < 80) // 80% 확률로 공격 성공
            {
                ProcessTurn(winMove);
                return;
            }

            // 2. [방어] 플레이어가 한 줄을 완성하려고 하는가?
            int blockMove = FindBestMove(1); // 플레이어 1 차단
            if (blockMove != -1 && aiRandom.Next(100) < 60) // 60% 확률로 방어 성공
            {
                ProcessTurn(blockMove);
                return;
            }

            // 3. [전략] 가운데(4번) 자리가 비었으면 우선순위 높임
            if (board[4] == 0 && aiRandom.Next(100) < 50)
            {
                ProcessTurn(4);
                return;
            }

            // 4. [기본] 나머지는 기존처럼 랜덤
            var emptyCells = board.Select((val, idx) => new { val, idx }).Where(x => x.val == 0).Select(x => x.idx).ToList();
            if (emptyCells.Count > 0)
            {
                ProcessTurn(emptyCells[aiRandom.Next(emptyCells.Count)]);
            }
        }

        // 특정 플레이어가 한 수를 뒀을 때 승리하는 위치를 찾는 '탐색 함수'
        private int FindBestMove(int player)
        {
            int[,] winCases = { {0,1,2}, {3,4,5}, {6,7,8}, {0,3,6}, {1,4,7}, {2,5,8}, {0,4,8}, {2,4,6} };

            for (int i = 0; i < 8; i++)
            {
                int c1 = winCases[i, 0];
                int c2 = winCases[i, 1];
                int c3 = winCases[i, 2];

                // 2칸이 해당 플레이어의 돌이고, 나머지 1칸이 빈칸인지 체크
                if (board[c1] == player && board[c2] == player && board[c3] == 0) return c3;
                if (board[c1] == player && board[c3] == player && board[c2] == 0) return c2;
                if (board[c2] == player && board[c3] == player && board[c1] == 0) return c1;
            }
            return -1; // 승리/차단할 자리가 없음
        }

        private void RefreshBoard()
        {
            for (int i = 0; i < 9; i++)
            {
                if (board[i] == 0)
                {
                    btnBoard[i].Text = (i + 1).ToString();
                    btnBoard[i].BackColor = Color.White;
                    btnBoard[i].ForeColor = Color.LightGray;
                }
                else
                {
                    btnBoard[i].Text = (board[i] == 1) ? "O" : "X";
                    btnBoard[i].ForeColor = Color.White;
                    btnBoard[i].BackColor = (board[i] == 1) ? Color.DodgerBlue : Color.Crimson;
                }

                // 사라질 돌 예고
                if (currentPlayer == 1 && historyP1.Count == 3 && i == historyP1.Peek())
                    btnBoard[i].BackColor = Color.Orange;
                if (currentPlayer == 2 && historyP2.Count == 3 && i == historyP2.Peek())
                    btnBoard[i].BackColor = Color.Orange;
            }
        }

        private bool CheckWinner(int player)
        {
            int[,] winCases = { {0,1,2}, {3,4,5}, {6,7,8}, {0,3,6}, {1,4,7}, {2,5,8}, {0,4,8}, {2,4,6} };
            for (int i = 0; i < 8; i++)
            {
                if (board[winCases[i, 0]] == player && board[winCases[i, 1]] == player && board[winCases[i, 2]] == player)
                    return true;
            }
            return false;
        }

        private void ResetGameData()
        {
            board = new int[9];
            historyP1.Clear();
            historyP2.Clear();
            currentPlayer = 1;
            isGameOver = false;
        }
    }
}