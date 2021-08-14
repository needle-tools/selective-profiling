using System;
using UnityEngine;

namespace _Tests.SelectiveProfilingTestComponent
{
	public class DeepProfileTest : MonoBehaviour
	{
		private void Update()
		{
			Level1();
		}

		private void Level1()
		{
			Level2();
		}

		private void Level2()
		{
			Level3();
		}

		private void Level3()
		{
			Level4();
		}

		private void Level4()
		{
			Level5();
		}

		private void Level5()
		{
			Level6();
		}

		private void Level6()
		{
			
		}

	}
}