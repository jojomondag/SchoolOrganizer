using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SchoolOrganizer.Src.Models.Students;

namespace SchoolOrganizer.Src.Services
{
    /// <summary>
    /// Mediator service for coordinating communication between Student Gallery, Add Student, and Profile Cards.
    /// Provides loose coupling between components through event-driven architecture.
    /// </summary>
    public class StudentCoordinatorService
    {
        // Singleton instance
        private static StudentCoordinatorService? _instance;
        public static StudentCoordinatorService Instance => _instance ??= new StudentCoordinatorService();

        // Private constructor for singleton pattern
        private StudentCoordinatorService()
        {
        }

        #region Events

        // Student Selection Events
        public event EventHandler<Student>? StudentSelected;
        public event EventHandler? StudentDeselected;

        // Student CRUD Events
        public event EventHandler? AddStudentRequested;
        public event EventHandler<Student>? StudentAdded;
        public event EventHandler<Student>? StudentUpdated;
        public event EventHandler<Student>? StudentDeleted;

        // Student Image Events
        public event EventHandler<Student>? StudentImageChangeRequested;
        public event EventHandler<(Student student, string imagePath, string? cropSettings, string? originalImagePath)>? StudentImageUpdated;

        // Student Edit Events
        public event EventHandler<Student>? EditStudentRequested;

        // Assignment Events
        public event EventHandler<Student>? ViewAssignmentsRequested;

        // Add Student Mode Events
        public event EventHandler? ManualEntryRequested;
        public event EventHandler? ClassroomImportRequested;
        public event EventHandler? AddStudentCompleted;
        public event EventHandler? AddStudentCancelled;

        #endregion

        #region Event Publishing Methods

        /// <summary>
        /// Publishes a student selection event
        /// </summary>
        public void PublishStudentSelected(Student student)
        {
            StudentSelected?.Invoke(this, student);
        }

        /// <summary>
        /// Publishes a student deselection event
        /// </summary>
        public void PublishStudentDeselected()
        {
            StudentDeselected?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Publishes an add student request event
        /// </summary>
        public void PublishAddStudentRequested()
        {
            AddStudentRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Publishes a student added event
        /// </summary>
        public void PublishStudentAdded(Student student)
        {
            StudentAdded?.Invoke(this, student);
        }

        /// <summary>
        /// Publishes a student updated event
        /// </summary>
        public void PublishStudentUpdated(Student student)
        {
            StudentUpdated?.Invoke(this, student);
        }

        /// <summary>
        /// Publishes a student deleted event
        /// </summary>
        public void PublishStudentDeleted(Student student)
        {
            StudentDeleted?.Invoke(this, student);
        }

        /// <summary>
        /// Publishes a student image change request event
        /// </summary>
        public void PublishStudentImageChangeRequested(Student student)
        {
            StudentImageChangeRequested?.Invoke(this, student);
        }

        /// <summary>
        /// Publishes a student image updated event
        /// </summary>
        public void PublishStudentImageUpdated(Student student, string imagePath, string? cropSettings = null, string? originalImagePath = null)
        {
            StudentImageUpdated?.Invoke(this, (student, imagePath, cropSettings, originalImagePath));
        }

        /// <summary>
        /// Publishes an edit student request event
        /// </summary>
        public void PublishEditStudentRequested(Student student)
        {
            EditStudentRequested?.Invoke(this, student);
        }

        /// <summary>
        /// Publishes a view assignments request event
        /// </summary>
        public void PublishViewAssignmentsRequested(Student student)
        {
            ViewAssignmentsRequested?.Invoke(this, student);
        }

        /// <summary>
        /// Publishes a manual entry request event
        /// </summary>
        public void PublishManualEntryRequested()
        {
            ManualEntryRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Publishes a classroom import request event
        /// </summary>
        public void PublishClassroomImportRequested()
        {
            ClassroomImportRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Publishes an add student completed event
        /// </summary>
        public void PublishAddStudentCompleted()
        {
            AddStudentCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Publishes an add student cancelled event
        /// </summary>
        public void PublishAddStudentCancelled()
        {
            AddStudentCancelled?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
